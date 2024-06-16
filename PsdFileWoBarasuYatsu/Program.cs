using CommandLine;
using PsdParser;
using PsdParser.AdditionalLayerInformations;
using System.Drawing;
using System.Runtime.InteropServices;
using System;
using System.Drawing.Imaging;

var parsed = Parser.Default.ParseArguments<Options>(args);
Options options;
if (parsed.Tag is ParserResultType.Parsed)
{
    options = parsed.Value;
}
else
{
    options = new Options();
    Console.WriteLine("----------");
    Console.WriteLine("引数が正しく設定されていません。対話モードで実行します。");
    Console.WriteLine("変換するpsdファイルのパスを入力するか、ファイルをD&Dしてください。");
    options.Input = Console.ReadLine() ?? "";
    Console.WriteLine("出力先フォルダのパスを入力してください。（任意）");
    options.Output = Console.ReadLine() ?? "";
    Console.WriteLine("レイヤー構造を無視して単一フォルダに出力しますか？（y/n）");
    options.SingleFolder = Console.ReadLine()?.ToLower() == "y";
    Console.WriteLine("出力する画像ファイル名の末尾に連番を付与しますか？（y/n）");
    options.Numbering = Console.ReadLine()?.ToLower() == "y";
    Console.WriteLine("レイヤー画像の透過部分を切り詰めますか？（y/n）");
    options.Crop = Console.ReadLine()?.ToLower() == "y";
    Console.WriteLine("----------");
}
if (!File.Exists(options.Input))
{
    Console.WriteLine($"psdファイルが見つかりません。");
    return;
}
Console.WriteLine($"psdファイル: {options.Input}");
var outputDirectory = options.OutputDirectoryPath;
using var psdFile = new PsdFile(options.Input);
var originalSize = new Size(psdFile.Header.Width, psdFile.Header.Height);
var layers = Enumerable.Range(0, int.MaxValue).Zip(psdFile.LayerAndMaskInformationSection.LayerInfo.Items).Reverse().Select(x=>new IndexedLayer(x.First, x.Second));

List<IndexedLayer> parentLayers = [];
foreach (var layer in layers)
{
    var layerType = layer.Value.Record.AdditionalLayerInformations.OfType<SectionDividerSetting>().FirstOrDefault()?.Type ?? SectionDividerSetting.LsctType.AnyOtherTypeOfLayer;
    if (layerType is SectionDividerSetting.LsctType.OpenedFolder or SectionDividerSetting.LsctType.ClosedFolder)
    {
        parentLayers.Add(layer);
    }
    else if (layerType is SectionDividerSetting.LsctType.BoundingSectionDivider)
    {
        if (parentLayers.Count > 0)
            parentLayers.Remove(parentLayers.Last());
    }
    else if (layer.Value.Image is not { Width: 0, Height: 0 })
    {
        var layerFilePath = CreateLayerImageFilePath(options.SingleFolder, options.Numbering, outputDirectory, parentLayers, layer);
        Console.WriteLine($"出力: {layerFilePath}");
        SaveLayerImage(layerFilePath, layer.Value, originalSize, options.Crop);
    }
}

void SaveLayerImage(string filePath, LayerRecordAndImage layer, Size originalSize, bool isCropping)
{
    var directoryPath = Path.GetDirectoryName(filePath);
    if (directoryPath is not null && !Directory.Exists(directoryPath))
        Directory.CreateDirectory(directoryPath);

    int width, height;
    byte[] buffer;
    if(isCropping)
    {
        width = layer.Image.Width;
        height = layer.Image.Height;
        buffer = layer.Image.Read();
    }
    else
    {
        width = originalSize.Width;
        height = originalSize.Height;
        buffer = new byte[width * height * 4];
        var layerBuffer = layer.Image.Read();
        for(int y = 0; y < layer.Image.Height; y++)
        {
            for(int x = 0; x < layer.Image.Width; x++)
            {
                var index = (y * layer.Image.Width + x) * 4;
                var layerIndex = index;
                var bufferIndex = ((y + layer.Record.Top) * width + (x + layer.Record.Left)) * 4;
                buffer[bufferIndex + 0] = layerBuffer[layerIndex + 0];
                buffer[bufferIndex + 1] = layerBuffer[layerIndex + 1];
                buffer[bufferIndex + 2] = layerBuffer[layerIndex + 2];
                buffer[bufferIndex + 3] = layerBuffer[layerIndex + 3];
            }
        }
    }

    using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
    bitmap.UnlockBits(data);
    bitmap.Save(filePath, ImageFormat.Png);
}

string GetLayerPath(IEnumerable<IndexedLayer> layers, bool containsId)
{
    if(!layers.Any())
        return "";
    return layers
        .Select(x=> GetLayerFileName(x, containsId))
        .Select(x => x.Trim())
        .Aggregate((x, y) => $"{x}\\{y}");
}

string EscapeFileName(string fileName)
{
    return Path.GetInvalidFileNameChars()
        .Aggregate(fileName, (x, y) => x.Replace(y, '_'));
}
string GetLayerFileName(IndexedLayer layer, bool containsId)
{
    var name = layer.Value.Record.AdditionalLayerInformations
        .OfType<UnicodeLayerName>()
        .FirstOrDefault()
        ?.Name ?? layer.Value.Record.LayerName;
    if (containsId && layer.Value.Record.AdditionalLayerInformations.OfType<LayerID>().FirstOrDefault() is LayerID layerId)
        name += $"{(layer.Index)}";
    return EscapeFileName(name);
}

string CreateLayerImageFilePath(bool isSingleFolder, bool containsId, string outputDirectory, IEnumerable<IndexedLayer> parentLayers, IndexedLayer layer)
{
    var relativePath = GetLayerPath(parentLayers, containsId);
    string filePath;
    if (isSingleFolder)
    {
        filePath = Path.Combine(
            outputDirectory,
            relativePath.Replace("\\", "_") + "_" + GetLayerFileName(layer, containsId) + ".png");
    }
    else
    {
        filePath = Path.Combine(
            outputDirectory,
            relativePath,
            GetLayerFileName(layer, containsId) + ".png");
    }
    return filePath;
}

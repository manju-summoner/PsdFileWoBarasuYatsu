using CommandLine;

class Options
{
    [Value(0, Required = true, HelpText = "変換するpsdファイルのパス")]
    public string Input { get; set; } = "";

    [Option('o', "output", Required = false, HelpText = "出力先フォルダのパス")]
    public string Output { get; set; } = "";

    [Option('s', "single", Required = false, HelpText = "レイヤー構造を無視して単一フォルダに出力する")]
    public bool SingleFolder { get; set; } = false;

    [Option('n', "numbering", Required = false, HelpText = "出力する画像ファイル名の先頭に連番を付与する")]
    public bool Numbering { get; set; } = false;

    [Option('c', "crop", Required = false, HelpText = "レイヤー画像の透過部分を切り詰める")]
    public bool Crop { get; set; } = false;


    public string OutputDirectoryPath
    {
        get
        {
            if(!string.IsNullOrEmpty(Output))
                return Output;
            var parent = Path.GetDirectoryName(Input);
            if (parent is null)
                return Output;
            var fileName = Path.GetFileNameWithoutExtension(Input);
            return Path.Combine(parent, fileName);
        }
    }
}
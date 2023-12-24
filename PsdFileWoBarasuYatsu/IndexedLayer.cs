using PsdParser;

class IndexedLayer(int index, LayerRecordAndImage layer)
{
    public int Index { get; set; } = index;
    public LayerRecordAndImage Value { get; set; } = layer;
}
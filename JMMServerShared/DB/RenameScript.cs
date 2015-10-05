namespace JMMServerModels.DB
{
    public class RenameScript
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Script { get; set; }
        public bool IsEnabledOnImport { get; set; }
    }
}

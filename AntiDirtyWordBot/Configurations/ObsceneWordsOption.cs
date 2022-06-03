namespace AntiDirtyWordBot.Configurations
{
    public class ObsceneWordsOption
    {
        public IEnumerable<Letter> Alphabet { get; set; }
        public IEnumerable<string> ObsceneWords { get; set; }
        public IEnumerable<Word> ExceptionWords { get; set; }
    }

    public class Letter
    {
        public char Key { get; set; }
        public IEnumerable<string> Value { get; set; }
    }

    public class Word
    {
        public string Key { get; set; }
        public IEnumerable<string> Value { get; set; }
    }
}

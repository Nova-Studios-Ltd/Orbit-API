namespace NovaAPI
{
    public struct Diamension
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Diamension(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
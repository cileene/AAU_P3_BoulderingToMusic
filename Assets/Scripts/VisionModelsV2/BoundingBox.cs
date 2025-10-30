namespace VisionModelsV2
{
    public class BoundingBox // changed to class from struct to allow passing by reference
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
        public string Label;
    }
}
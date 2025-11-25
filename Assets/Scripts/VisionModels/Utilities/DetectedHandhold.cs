namespace VisionModels.Utilities
{
    public class DetectedHandhold
    {
        public BoundingBox Box;
        public string Label;
        public int FramesSinceLastSeen;
        public int Id;
        public bool LeftHasBeenDetected;
        public bool RightHasBeenDetected;
    }
}
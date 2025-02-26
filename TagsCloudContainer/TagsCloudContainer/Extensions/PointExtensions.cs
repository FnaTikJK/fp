﻿namespace TagsCloudVisualization
{
    public static class PointExtensions
    {
        public static Point Plus(this Point selfPoint, Point otherPoint)
        {
            return new Point(selfPoint.X + otherPoint.X, selfPoint.Y + otherPoint.Y);
        }

        public static Point Minus(this Point selfPoint, Point otherPoint)
        {
            return new Point(selfPoint.X - otherPoint.X, selfPoint.Y - otherPoint.Y);
        }
    }
}

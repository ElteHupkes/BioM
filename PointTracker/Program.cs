using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.Vision.Tracking;
using Image = Accord.Imaging.Image;

namespace PointTracker
{
    /// <summary>
    /// If we call the points:
    /// a) Hand
    /// b) Elbow
    /// c) Shoulder
    /// d) Hip
    /// e) Knee
    /// f) Ankle
    /// g) Foot
    /// 
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Number of pixels per centimeter in the HD frame
        /// </summary>
        private const float HdPixelsPerCentimeter = 4.3f;
        
        /// <summary>
        /// Number of pixels per centimeter in the SD frame
        /// </summary>
        private const float SdPixelsPerCentimeter = 1.75f;
        
        /// <summary>
        /// Total number of frames
        /// </summary>
        private const int NumFrames = 20;
        
        /// <summary>
        /// Crop of the final output image in HD
        /// </summary>
        private static readonly RectangleF HDCrop = new RectangleF(550, 0, 950, 950);
        private static readonly RectangleF SDCrop = new RectangleF(250, 0, 350, 380);
        
        /// <summary>
        /// Bounding boxes in frame 1, SD frames
        /// </summary>
        private static readonly (string, RectangleF)[] BoxesSD = {
            // Hand 484 67 500 79
            ("Wrist", RectPoints(484, 67, 500, 79)),
            
            // Elbow 436 67 451 79
            ("Elbow", RectPoints(436, 67, 451, 79)),
            
            // Shoulder 384 61 398 72
            ("Shoulder", RectPoints(384, 61, 398, 72)),
            
            // Hip 373 165 387 179
            ("Hip", RectPoints(373, 165, 387, 179)),
            
            // Knee 381 254 396 268
            ("Knee", RectPoints(381, 254, 396, 268)),
            
            // Ankle 362 341 379 352
            ("Ankle", RectPoints(362, 341, 379, 352)),
            
            // Toe 391 355 405 366
            ("Foot", RectPoints(391, 355, 405, 366)),
        };

        /// <summary>
        /// Template boxes in the HD frame
        /// </summary>
        private static readonly (string, RectangleF)[] BoxesHD = {
            // Hand 1175 210 1204 234
            ("Wrist", RectPoints(1175, 210, 1204, 234)),
            
            // Elbow 1057 205 1082 225 (ENLARGED)
            ("Elbow", RectPoints(1052, 200, 1087, 230)),
            
            // Shoulder 935 168 963 189
            ("Shoulder", RectPoints(935, 168, 963, 189)),
            
            // Hip 905 402 935 430
            ("Hip", RectPoints(905, 402, 935, 430)),
            
            // Knee 927 625 951 650
            ("Knee", RectPoints(927, 625, 951, 650)),
            
            // Ankle 895 840 916 856
            ("Ankle", RectPoints(895, 840, 916, 856)),
            
            // Foot 961 873 984 889
            ("Foot", RectPoints(961, 873, 984, 889)),
        };
        
        /// <summary>
        /// Pen to annotate the rectangles
        /// </summary>
        private static readonly Pen RectPen = new Pen(Color.Red, 1);
        
        /// <summary>
        /// Center point pen
        /// </summary>
        private static readonly Pen PointPen = new Pen(Color.Aqua, 1);
        
        /// <summary>
        /// Stick figure pen
        /// </summary>
        private static readonly Pen StickPen = new Pen(Color.Chartreuse, 1);
        
        /// <summary>
        /// Rectangle from four annotation points
        /// </summary>
        /// <param name="xmin"></param>
        /// <param name="ymin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymax"></param>
        /// <returns></returns>
        private static RectangleF RectPoints(int xmin, int ymin, int xmax, int ymax)
        {
            var width = xmax - xmin;
            var height = ymax - ymin;
            
            return new RectangleF(xmin, ymin, width, height);
        }
        
        /// <summary>
        /// Main code
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {   
            var pictureDir = args[0];
            var frame1Path = Path.Combine(pictureDir, "01.jpg");
            var frame1 = Image.FromFile(frame1Path);

            var boxes = BoxesHD;
            var crop = HDCrop;
            var ppcm = HdPixelsPerCentimeter;
            
            if (args[1] == "sd")
            {
                boxes = BoxesSD;
                crop = SDCrop;
                ppcm = SdPixelsPerCentimeter;
            }

            var names = new List<string>();
            var trackers = new List<MatchingTracker>();
            var rects = new List<RectangleF>();
            
            foreach (var (name, box) in boxes)
            {
                var template = frame1.Clone(box, frame1.PixelFormat);
                var tracker = new MatchingTracker()
                {
                    Template = template,
                    Threshold = 0.8,
                    RegistrationThreshold = 0.99
                };

                trackers.Add(tracker);
                rects.Add(box);
                names.Add(name);
            }
            
            // Class that calculates partial centers of mass
            var pcmCalculator = new CmCalculator();
            
            for (var i = 1; i <= NumFrames; ++i)
            {
                var ii = $"{i}".PadLeft(2, '0');
                var file = $"{ii}.jpg";
                var fullPath = Path.Combine(pictureDir, file);
                var frame = Image.FromFile(fullPath);

                var totalWidth = frame.Width;
                var totalHeight = frame.Height;
                var tw = 0.2f * totalWidth;
                var th = 0.2f * totalHeight;
                
                // Track all objects in the frame
                using (var unmanaged = UnmanagedImage.FromManagedImage(frame))
                {                    
                    for (var j = 0; j < trackers.Count; j++)
                    {
                        var rect = rects[j];
                        var tracker = trackers[j];
                        
                        // Set the search window to 5 times the current size in each direction
                        var center = rect.Center();
                        tracker.SearchWindow = new Rectangle(IntRound(center.X - 0.5f * tw),
                            IntRound(center.Y - 0.5f * th), IntRound(tw), IntRound(th));

                        tracker.ProcessFrame(unmanaged);

                        if (tracker.TrackingObject.IsEmpty)
                        {
                            Console.Error.WriteLine($"EMPTY OBJECT, {names[j]} @ frame {i}");
                        }
                        
                        rects[j] = tracker.TrackingObject.Rectangle;
                    }
                }

                pcmCalculator.Update(names, rects);
                
                // Draw resulting rectangles
                using (var g = Graphics.FromImage(frame))
                {
                    foreach (var rect in rects)
                    {
                        //g.DrawRectangle(RectPen, rect.X, rect.Y, rect.Width, rect.Height);
                        DrawPoint(g, Brushes.Cyan, rect.Center(), 2);
                    }

                    var points = rects.Select(r => r.Center()).ToArray();
                    for (var j = 0; j < points.Length - 1; j++)
                    {
                        g.DrawLine(StickPen, points[j], points[j+1]);
                    }

                    var (pcms, gcm) = pcmCalculator.Get();
                    foreach (var kv in pcms)
                    {
                        DrawPoint(g, Brushes.Red, kv.Value, 2);
                    }
                    
                    DrawPoint(g, Brushes.Orange, gcm, 4);
                }
                
                var outFile = Path.Combine(pictureDir, "out", $"{ii}_annotated.jpg");
                var frameCrop = frame.Clone(crop, frame.PixelFormat);
                //DrawGrid(frameCrop, 20 * ppcm);
                frameCrop.Save(outFile);
            }
        }

        private static void DrawPoint(Graphics g, Brush brush, PointF center, float radius = 1)
        {
            var cx = center.X;
            var cy = center.Y;
            g.FillEllipse(brush, cx - radius, cy - 0.5f * radius, 2 * radius, 2 * radius);
        }
        
        /// <summary>
        /// Draws a grid over the given graphics image
        /// </summary>
        /// <param name="g"></param>
        /// <param name="spacing"></param>
        private static void DrawGrid(Bitmap b, float spacing)
        {
            var aq = Color.Aquamarine;
            var color = Color.FromArgb(150, aq.R, aq.G, aq.B);
            var gridPen = new Pen(color, 1);
            using (var g = Graphics.FromImage(b))
            {
                for (float x = 0; x < b.Width; x += spacing)
                {
                    g.DrawLine(gridPen, x, 0, x, b.Height);
                }
                
                for (float y = 0; y < b.Height; y += spacing)
                {
                    g.DrawLine(gridPen, 0, y, b.Width, y);
                }
            }
        }
        
        private static int IntRound(double f)
        {
            return (int) Math.Round(f);
        }
    }
}
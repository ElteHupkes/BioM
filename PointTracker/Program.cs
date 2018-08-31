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
        private static readonly Pen StickPen = new Pen(Color.Chartreuse, 2);
        
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
            var outDir = Path.Combine(pictureDir, "out");
            var csvPath = outDir;
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

            var csv = new DataWriter(csvPath, crop, ppcm, "global", "ankle", 
                "knee", "hip");
            
            var names = new List<string>();
            var trackers = new List<MatchingTracker>();
            var rects = new List<RectangleF>();
            
            foreach (var (name, box) in boxes)
            {
                var template = frame1.Clone(box, frame1.PixelFormat);
                var tracker = new MatchingTracker
                {
                    Template = template,
                    Threshold = 0.8,
                    RegistrationThreshold = 0.99
                };

                trackers.Add(tracker);
                
                // Adjust the box to track to the crop we'll apply on the image
                var adjustedBox = new RectangleF(box.Left - crop.Left, box.Top - crop.Top, box.Width, box.Height);
                rects.Add(adjustedBox);
                names.Add(name);
            }
            
            // Class that calculates partial centers of mass
            var pcmCalculator = new CmCalculator();
            
            for (var i = 1; i <= NumFrames; ++i)
            {   
                var ii = $"{i}".PadLeft(2, '0');
                var file = $"{ii}.jpg";
                var fullPath = Path.Combine(pictureDir, file);

                Bitmap frameCrop;
                using (var frame = Image.FromFile(fullPath))
                {
                    frameCrop = frame.Clone(crop, frame.PixelFormat);
                }
                
                // Create the output image that we'll draw on, which
                // is a fully white image.
                var output = new Bitmap(frameCrop.Width, frameCrop.Height, frameCrop.PixelFormat);
                using (var g = Graphics.FromImage(output))
                {
                    g.FillRectangle(Brushes.White, 0, 0, output.Width, output.Height);
                }
                
                var totalWidth = frameCrop.Width;
                var totalHeight = frameCrop.Height;
                var tw = 0.25f * totalWidth;
                var th = 0.25f * totalHeight;
                
                // Track all objects in the frame
                using (var unmanaged = UnmanagedImage.FromManagedImage(frameCrop))
                {                    
                    for (var j = 0; j < trackers.Count; j++)
                    {
                        var rect = rects[j];
                        var tracker = trackers[j];
                        
                        // Set the search window to its position + 25% the total width / height in
                        // each direction.
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
                var pcms = pcmCalculator.GetPcms();
                var (gcm, gWeight) = pcmCalculator.GetGcm();
                var (anklePcm, ankleWeight) = pcmCalculator.GetAnklePcm();
                var (kneePcm, kneeWeight) = pcmCalculator.GetKneePcm();
                var (hipPcm, hipWeight) = pcmCalculator.GetHipPcm();
                
                csv.WriteFrame("global", i, gcm, gWeight);
                
                csv.WriteFrame("ankle", i, anklePcm, ankleWeight, pcmCalculator.GetLocation("Ankle"));
                csv.WriteFrame("knee", i, kneePcm, kneeWeight, pcmCalculator.GetLocation("Knee"));
                csv.WriteFrame("hip", i, hipPcm, hipWeight, pcmCalculator.GetLocation("Hip"));
                
                // Draw resulting rectangles
                using (var g = Graphics.FromImage(output))
                {
                    // Draw the frame image over the output image
                    g.DrawImageUnscaled(frameCrop, 0, 0);
                    
                    // Detected points
//                    foreach (var rect in rects)
//                    {
//                        g.DrawRectangle(RectPen, rect.X, rect.Y, rect.Width, rect.Height);
//                        DrawPoint(g, Brushes.Cyan, rect.Center(), 2);
//                    }

                    // Draw the stick figure
                    var points = rects.Select(r => r.Center()).ToArray();
                    for (var j = 0; j < points.Length - 1; j++)
                    {
                        g.DrawLine(StickPen, points[j], points[j+1]);
                    }

                    // PCMs for each separate body part
//                    foreach (var kv in pcms)
//                    {
//                        DrawPoint(g, Brushes.Red, kv.Value, 2);
//                    }
                    
                    // Ankle, knee, hip PCMs
                    DrawPoint(g, Brushes.Orange, anklePcm, 4);
                    DrawPoint(g, Brushes.Green, kneePcm, 4);
                    DrawPoint(g, Brushes.Blue, hipPcm, 4);
     
                    // Global center of mass
                    DrawPoint(g, Brushes.Red, gcm, 5);
                }
                
                var outFile = Path.Combine(outDir, $"{ii}_annotated.jpg");
                DrawGrid(output, 40, ppcm);
                output.Save(outFile);
            }
            
            csv.Dispose();
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
        /// <param name="b"></param>
        /// <param name="cmSpacing"></param>
        /// <param name="pixelsPerCm"></param>
        private static void DrawGrid(Bitmap b, int cmSpacing, float pixelsPerCm)
        {
            var spacing = pixelsPerCm * cmSpacing;
            var aq = Color.Aquamarine;
            var color = Color.FromArgb(150, aq.R, aq.G, aq.B);
            var gridPen = new Pen(color, 1);
            var font = new Font("Menlo", 6);
            
            using (var g = Graphics.FromImage(b))
            {
                var xs = 0;
                for (float x = 0; x < b.Width; x += spacing)
                {
                    g.DrawLine(gridPen, x, 0, x, b.Height);
                    if (xs > 0)
                    {
                        g.DrawString($"{xs/100.0:0.0}m", font, Brushes.Black, x + 5, b.Height - 10);   
                    }
                    xs += cmSpacing;
                }

                var ys = 0;
                for (float y = b.Height; y >= 0; y -= spacing)
                {
                    g.DrawLine(gridPen, 0, y, b.Width, y);
                    g.DrawString($"{ys/100.0:0.0}m", font, Brushes.Black, 5, y - 10);
                    
                    ys += cmSpacing;
                }
            }
        }
        
        private static int IntRound(double f)
        {
            return (int) Math.Round(f);
        }
    }
}
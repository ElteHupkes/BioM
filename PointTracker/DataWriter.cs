using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PointTracker
{
    public class DataWriter : IDisposable
    {
        /// <summary>
        /// Frame duration in seconds
        /// </summary>
        private const float FrameDuration = 0.22f;
        
        /// <summary>
        /// Crop region of the picture, allows us to calculate
        /// the adjusted coordinates. Given GCM coordinates will
        /// be in image coordinates, we recalculate in Cartesian
        /// coordinates.
        /// </summary>
        private readonly RectangleF _crop;

        /// <summary>
        /// Number of pixels in one cm
        /// </summary>
        private readonly float _ppm;

        /// <summary>
        /// Last recorded centers of mass
        /// </summary>
        private readonly Dictionary<string, PointF?> _lastCms = 
            new Dictionary<string, PointF?>();

        /// <summary>
        /// Last calculated GCM velocities
        /// </summary>
        private readonly Dictionary<string, PointF?> _lastVcms = 
            new Dictionary<string, PointF?>();

        /// <summary>
        /// Named data output streams
        /// </summary>
        private readonly Dictionary<string, StreamWriter> _streams;
        
        public DataWriter(string path, RectangleF crop, 
            double pixelsPerCentimeter, params string[] bodies)
        {
            _crop = crop;
            _ppm = (float)(pixelsPerCentimeter * 100);

            _streams = bodies.ToDictionary(b => b, body => 
                new StreamWriter(Path.Combine(path, body + ".csv")));

            foreach (var kv in _streams)
            {
                kv.Value.WriteLine("frame,cm_x,cm_y,cm_dx,cm_dy,cm_vdx,cm_vdy,cm_adx,cm_ady,fg,lever_arm");
                _lastCms[kv.Key] = null;
                _lastVcms[kv.Key] = null;
            }
        }

        /// <summary>
        /// Writes a single frame for a body
        /// </summary>
        /// <param name="body"></param>
        /// <param name="frame"></param>
        /// <param name="cm"></param>
        /// <param name="weight"></param>
        /// <param name="leverReference"></param>
        public void WriteFrame(string body, int frame, PointF cm, double weight, PointF? leverReference = null)
        {
            var stream = _streams[body];
            
            cm = Reframe(cm);

            var lastCm = _lastCms[body];
            var lastVcm = _lastVcms[body];
            
            var dd = lastCm.HasValue ? cm.Sub(lastCm.Value) : new PointF(0, 0);
            var dv = dd.Div(FrameDuration);

            var da = lastVcm.HasValue ? dv.Sub(lastVcm.Value).Div(FrameDuration) : new PointF(0, 0);

            _lastCms[body] = cm;
            _lastVcms[body] = dv;

            var fg = weight * (da.Y + 9.81f);

            double leverArm = 0;
            if (leverReference.HasValue)
            {
                var lr = Reframe(leverReference.Value);
                
                // https://www.quora.com/What-is-a-lever-arm-physics
                // The lever arm is the perpendicular distance between the line of force
                // and the center of rotation.
                // The line of force is always pointing down, so we can get our lever arm
                // from just the x-coordinate of our center of mass and lever arm reference.
                leverArm = Math.Abs(lr.X - cm.X);
            }
            
            stream.WriteLine($"{frame},{Nf(cm.X)},{Nf(cm.Y)},{Nf(dd.X)},{Nf(dd.Y)},{Nf(dv.X)},{Nf(dv.Y)}" +
                           $",{Nf(da.X)},{Nf(da.Y)},{fg},{Nf(leverArm)}");
        }

        /// <summary>
        /// Number formatter
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private string Nf(double d)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00000}", d);
        }
        
        /// <summary>
        /// Point reframer
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private PointF Reframe(PointF p)
        {
            return new PointF(p.X, _crop.Height - p.Y).Div(_ppm);
        }

        public void Dispose()
        {
            foreach (var kv in _streams)
            {
                kv.Value.Flush();
                kv.Value.Dispose();
            }
        }
    }
}
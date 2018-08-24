﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Accord.Vision.Tracking;

namespace PointTracker
{
    /// <summary>
    /// We want to determine PCMs:
    /// - Head
    /// - Upper arm
    /// - Forearm
    /// - Hand
    /// - Trunk
    /// - Thigh
    /// - Shank
    /// - Foot
    ///
    /// Based on the picture we define:
    /// 
    /// - Head
    /// The head is about 40 by 40 pixels, the shoulder thingy
    /// located at about 10px from the right, so the PCM of the
    /// head is located at about:
    /// shoulder + (0.5894 * 40, -17)
    ///
    /// - Upper arm
    /// Is located some 57.54% of the segment from the shoulder to the elbow.
    /// shoulder + 0.5754(elbow - shoulder)
    ///
    /// - Forearm
    /// Located 45.59% of the segment from the elbow to the wrist:
    /// elbow + 0.4559 * (wrist - elbow)
    ///
    /// - Hand
    /// Located 74.74% of the segment from the wrist to the tip of the
    /// middle finger, which is about 35px to the right:
    /// Wrist + 0.7474 * (35, 0)
    /// 
    /// - Trunk
    /// Located 41.51% of the mid-shoulder to the mid-hip:
    /// Shoulder + 0.4151 * (Hip - Shoulder)
    /// 
    /// - Thigh
    /// Located 36.12% of the segment from the HJC to the KJC:
    /// Hip + 0.3612 * (Knee - Hip)
    /// 
    /// - Shank
    /// Located 44.16% of the segment from the KJC to the AJC:
    /// Knee + 0.4416 * (Ankle - Knee)
    /// 
    /// - Foot
    /// Heel = 40px to the left from foot
    /// Toe = 10px to the right from foot
    /// So the PCM is located at:
    /// Foot + (-40, 0) + 0.4014 * (50, 0)
    /// </summary>
    public class CmCalculator
    {
        /// <summary>
        /// Total weight in kilograms
        /// </summary>
        private const double TotalWeight = 69;
        
        /// <summary>
        /// Last known locations
        /// </summary>
        private readonly Dictionary<string, PointF> _locs = new Dictionary<string, PointF>();

        private readonly Dictionary<string, double> _weights;
        
        public CmCalculator()
        {
            var weights = new Weights(TotalWeight);
            
            _weights = new Dictionary<string, double>()
            {
                {"Head", weights.Head},
                {"UpperArm", weights.UpperArms},
                {"Forearm", weights.Forearms},
                {"Hand", weights.Hands},
                {"Trunk", weights.Trunk},
                {"Thigh", weights.UpperLegs},
                {"Shank", weights.LowerLegs},
                {"Foot", weights.Feet},
            };
        }
        
        /// <summary>
        /// Returns a dictionary with all the partial centers of mass,
        /// as well as a separate global center of mass point.
        /// </summary>
        /// <returns></returns>
        public (Dictionary<string, PointF>, PointF) Get()
        {
            var lWrist = _locs["Wrist"];
            var lElbow = _locs["Elbow"];
            var lShoulder = _locs["Shoulder"];
            var lHip = _locs["Hip"];
            var lKnee = _locs["Knee"];
            var lAnkle = _locs["Ankle"];
            var lFoot = _locs["Foot"];
            
            // Head:
            var head = lShoulder.Add(new PointF(-12, -0.5894f * 40));

            // Upper arm
            var upperArm = Walk(lShoulder, lElbow, 0.5754f);
            
            // Forearm
            var forearm = Walk(lElbow, lWrist, 0.4559f);
            
            // Hand
            var hand = lWrist.Add(new PointF(0.7474f * 35, 0));
            
            // Trunk
            var trunk = Walk(lShoulder, lHip, 0.4151f);

            // Thigh
            var thigh = Walk(lHip, lKnee, 0.3612f);
            
            // Shank
            var shank = Walk(lKnee, lAnkle, 0.4416f);
            
            // Foot
            var foot = lFoot.Add(new PointF(0.4014f * 50 - 40, 0));
            
            var pcms = new Dictionary<string, PointF>()
            {
                {"Head", head},
                {"UpperArm", upperArm},
                {"Forearm", forearm},
                {"Hand", hand},
                {"Trunk", trunk},
                {"Thigh", thigh},
                {"Shank", shank},
                {"Foot", foot}
            };

            var gcm = pcms.Aggregate(new PointF(0, 0), (current, kvp) =>
            {
                var weight = _weights[kvp.Key];
                return current.Add(kvp.Value.Mult((float) weight));
            }).Div((float)TotalWeight);

            return (pcms, gcm);
        }

        private static PointF Walk(PointF from, PointF to, float frac)
        {
            return from.Add(to.Sub(from).Mult(frac));
        }
        
        /// <summary>
        /// 
        /// </summary>
        public void Update(List<string> names, List<RectangleF> rects)
        {
            for (var i = 0; i < names.Count; ++i)
            {
                _locs[names[i]] = rects[i].Center();
            }
        }
    }
}
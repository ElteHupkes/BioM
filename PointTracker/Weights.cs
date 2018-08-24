namespace PointTracker
{
    public class Weights
    {
        /// <summary>
        /// Total weight
        /// </summary>
        public double Total { get; }

        public double Head => 0.07 * Total;

        public double UpperArm => 0.04 * Total;
        public double UpperArms => 2 * UpperArm;

        public double Forearm => 0.025 * Total;
        public double Forearms => 2 * Forearm;

        public double Hand => 0.005 * Total;
        public double Hands => 2 * Hand;

        public double Trunk => 0.43 * Total;

        public double UpperLeg => 0.12 * Total;
        public double UpperLegs => 2 * UpperLeg;

        public double LowerLeg => 0.045 * Total;
        public double LowerLegs => 2 * LowerLeg;

        public double Foot => 0.015 * Total;
        public double Feet => 2 * Foot;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="total">Total weight in kilograms</param>
        public Weights(double total)
        {
            Total = total;
        }
    }
}
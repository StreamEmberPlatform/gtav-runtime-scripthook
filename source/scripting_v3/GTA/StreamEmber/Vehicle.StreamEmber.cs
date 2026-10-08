//
// StreamEmber: vehicle state found while porting ChaosModV (see docs/StreamEmber-API.md).
//

namespace GTA
{
    public sealed partial class Vehicle
    {
        /// <summary>
        /// StreamEmber: gets or sets the vehicle's "out of control" state bit (it keeps rolling and does not respond
        /// to the driver; unlike <c>SET_VEHICLE_OUT_OF_CONTROL</c> nobody is killed and nothing explodes).
        /// Helicopters, planes and blimps are ignored (writing it to them can crash the game).
        /// </summary>
        public bool IsOutOfControlState
        {
            get
            {
                if (!TryGetMemoryAddress(out System.IntPtr address))
                {
                    return false;
                }

                return SHVDN.StreamEmberMemory.GetVehicleOutOfControl(address);
            }
            set
            {
                if (!TryGetMemoryAddress(out System.IntPtr address))
                {
                    return;
                }

                VehicleClass vehicleClass = ClassType;
                if (vehicleClass == VehicleClass.Helicopters || vehicleClass == VehicleClass.Planes
                    || Model.Hash == unchecked((int)0xF7004C86) /* blimp */)
                {
                    return;
                }

                SHVDN.StreamEmberMemory.SetVehicleOutOfControl(address, value);
            }
        }

        /// <summary>
        /// StreamEmber: gets whether the brake is pressed (the vehicle's brake input, any driver: player or AI).
        /// </summary>
        public bool IsBrakePressed
        {
            get
            {
                if (!TryGetMemoryAddress(out System.IntPtr address))
                {
                    return false;
                }

                return SHVDN.StreamEmberMemory.GetVehicleBrakeInput(address) != 0f;
            }
        }

        /// <summary>
        /// StreamEmber: multiplies the vehicle's render and physics matrices by <paramref name="multiplier"/>
        /// (a visual shrink / grow, ChaosModV "Tiny Vehicles"). The game rebuilds the matrices every frame from
        /// the position and rotation, so call it every tick while the effect should last.
        /// </summary>
        public void ScaleMatrix(float multiplier)
        {
            if (!TryGetMemoryAddress(out System.IntPtr address))
            {
                return;
            }

            SHVDN.StreamEmberMemory.ScaleEntityMatrices(address, multiplier);
        }
    }
}

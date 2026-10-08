//
// StreamEmber: entity helpers found while porting ChaosModV (see docs/StreamEmber-API.md).
//

using GTA.Math;

namespace GTA
{
    public abstract partial class Entity
    {
        /// <summary>
        /// StreamEmber: gets whether this entity currently has a physics collider (is physically active).
        /// Calls the game's <c>CEntity::GetColliderNonConst</c>.
        /// </summary>
        public bool HasCollider
        {
            get
            {
                if (!TryGetMemoryAddress(out System.IntPtr address))
                {
                    return false;
                }

                return SHVDN.StreamEmberMemory.EntityHasCollider(address);
            }
        }

        /// <summary>
        /// StreamEmber: <c>APPLY_FORCE_TO_ENTITY</c> that is skipped when the physics simulator is short on collider
        /// slots and this entity is not physically active yet (applying force activates physics). Returns whether the
        /// force was applied. Same parameters as the native.
        /// </summary>
        public bool ApplyForceSafe(ForceType forceType, Vector3 force, Vector3 offset, int boneIndex,
            bool isDirectionRelative, bool ignoreUpVector, bool isForceRelative, bool p12 = false, bool p13 = true)
        {
            if (!SHVDN.StreamEmberMemory.IsFreeToActivatePhysics && !HasCollider)
            {
                return false;
            }

            Native.Function.Call(Native.Hash.APPLY_FORCE_TO_ENTITY, Handle, (int)forceType, force.X, force.Y, force.Z,
                offset.X, offset.Y, offset.Z, boneIndex, isDirectionRelative, ignoreUpVector, isForceRelative, p12, p13);
            return true;
        }

        /// <summary>
        /// StreamEmber: <c>APPLY_FORCE_TO_ENTITY_CENTER_OF_MASS</c> with the same collider-slot check as
        /// <see cref="ApplyForceSafe"/>.
        /// </summary>
        public bool ApplyForceCenterOfMassSafe(ForceType forceType, Vector3 force, bool p5, bool isDirectionRelative,
            bool isForceRelative, bool p8)
        {
            if (!SHVDN.StreamEmberMemory.IsFreeToActivatePhysics && !HasCollider)
            {
                return false;
            }

            Native.Function.Call(Native.Hash.APPLY_FORCE_TO_ENTITY_CENTER_OF_MASS, Handle, (int)forceType, force.X, force.Y,
                force.Z, p5, isDirectionRelative, isForceRelative, p8);
            return true;
        }

        /// <summary>
        /// StreamEmber: the bone index of a fragment group (the reverse of <see cref="EntityBone.FragmentGroupIndex"/>),
        /// or -1. Use with <see cref="DetachFragmentPart(int)"/> / <c>Bones[i]</c> (ChaosModV GetBoneIndexByFragIndex).
        /// </summary>
        public int GetFragmentGroupBoneIndex(int fragmentGroupIndex)
        {
            if (fragmentGroupIndex < 0 || !TryGetMemoryAddress(out System.IntPtr address))
            {
                return -1;
            }

            int boneCount = Bones.Count;
            for (int i = 0; i < boneCount; i++)
            {
                if (SHVDN.NativeMemory.GetFragmentGroupIndexByEntityBoneIndex(address, i) == fragmentGroupIndex)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace Varneon.VUdon.VehicleControls
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Lever : UdonSharpBehaviour
    {
        [SerializeField]
        private UdonBehaviour target;

        [SerializeField]
        private string variableName;

        [SerializeField]
        private VRC_Pickup.PickupHand handMode;

        [SerializeField]
        private Transform leverRoot, leverGrabPoint;

        [SerializeField]
        private float range = 20f;

        [SerializeField]
        private float inputExponent = 4f;

        private bool rightHanded;

        private Vector3 handPos;

        private float leverRotationOffset;

        private bool grabLeft;
        private bool grabRight;

        private bool GrabLeft
        {
            set
            {
                if (grabLeft != value)
                {
                    grabLeft = value;

                    if (HoldingLever)
                    {
                        if (!value) { HoldingLever = false; }
                    }
                    else
                    {
                        waitingForLeverHoldingCheck = true;
                    }
                }
            }
        }

        private bool GrabRight
        {
            set
            {
                if (grabRight != value)
                {
                    grabRight = value;

                    if (HoldingLever)
                    {
                        if (!value) { HoldingLever = false; }
                    }
                    else
                    {
                        waitingForLeverHoldingCheck = true;
                    }
                }
            }
        }

        private bool waitingForLeverHoldingCheck;

        private bool holdingLever;

        private bool HoldingLever
        {
            set
            {
                if (holdingLever != value)
                {
                    holdingLever = value;

                    centeringSettled = false;

                    if (!value) { target.SetProgramVariable(variableName, 0f); }
                }
            }
            get => holdingLever;
        }

        private bool centeringSettled;

        private VRCPlayerApi localPlayer;

        private void Start()
        {
            rightHanded = handMode == VRC_Pickup.PickupHand.Right;

            if (!(localPlayer = Networking.LocalPlayer).IsUserInVR()) { Destroy(this); }
        }

        private void LateUpdate()
        {
            if (waitingForLeverHoldingCheck)
            {
                if (rightHanded)
                {
                    handPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                }
                else
                {
                    handPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                }

                if (GetHandDistanceToLever() < 0.1f)
                {
                    holdingLever = true;

                    Vector3 localHandPos = transform.InverseTransformPoint(handPos);

                    leverRotationOffset = Mathf.Atan2(localHandPos.y, -localHandPos.z) * Mathf.Rad2Deg;
                }

                waitingForLeverHoldingCheck = false;
            }

            if (holdingLever)
            {
                if (rightHanded)
                {
                    handPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                }
                else
                {
                    handPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                }

                Vector3 localHandPos = transform.InverseTransformPoint(handPos);

                float angle = Mathf.Clamp(Mathf.Atan2(localHandPos.y, -localHandPos.z) * Mathf.Rad2Deg - leverRotationOffset, -range, range);

                leverRoot.localEulerAngles = new Vector3(angle, 0f, 0f);

                target.SetProgramVariable(variableName, Mathf.Pow(Mathf.Abs(angle) / range, inputExponent) * Mathf.Sign(angle));

                float amplitude = Mathf.Abs(angle) / range;

                localPlayer.PlayHapticEventInHand(handMode, 0.5f, amplitude, amplitude);
            }
            else if (centeringSettled == false)
            {
                Quaternion rotationCurrent = leverRoot.rotation;
                Quaternion rotationTarget = transform.rotation;

                if (Quaternion.Angle(rotationCurrent, rotationTarget) < float.Epsilon) { centeringSettled = true; }

                leverRoot.rotation = Quaternion.RotateTowards(rotationCurrent, rotationTarget, Time.deltaTime * Quaternion.Angle(rotationCurrent, rotationTarget) * 5f);
            }
        }

        private float GetHandDistanceToLever()
        {
            return Vector3.Distance(handPos, leverGrabPoint.position);
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if (!value) { target.SendCustomEvent(string.Format("_Reset{0}", variableName)); }

            switch (args.handType)
            {
                case HandType.LEFT:
                    if (!rightHanded) { GrabLeft = value; }
                    break;
                case HandType.RIGHT:
                    if (rightHanded) { GrabRight = value; }
                    break;
            }
        }
    }
}

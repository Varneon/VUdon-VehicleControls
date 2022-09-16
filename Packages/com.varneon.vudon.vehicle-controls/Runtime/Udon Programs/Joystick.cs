using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace Varneon.VUdon.VehicleControls
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Joystick : UdonSharpBehaviour
    {
        [SerializeField]
        private UdonBehaviour target;

        [SerializeField]
        private string variableName;

        [SerializeField]
        private VRC_Pickup.PickupHand handMode;

        [SerializeField]
        private Transform joystickRoot, joystickGrabPoint;

        [SerializeField]
        private Vector2 range = new Vector2(90f, 90f);

        [SerializeField]
        private float inputExponent = 4f;

        private bool rightHanded;

        private string thumbstickHorizontalAxis;

        private string thumbstickVerticalAxis;

        private Vector3 handPos;

        private Quaternion
            handRot,
            joystickRotationOffset;

        private bool grabLeft;
        private bool grabRight;

        private bool GrabLeft
        {
            set
            {
                if (grabLeft != value)
                {
                    grabLeft = value;

                    if (HoldingJoystick)
                    {
                        if (!value) { HoldingJoystick = false; }
                    }
                    else
                    {
                        waitingForJoystickHoldingCheck = true;
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

                    if (HoldingJoystick)
                    {
                        if (!value) { HoldingJoystick = false; }
                    }
                    else
                    {
                        waitingForJoystickHoldingCheck = true;
                    }
                }
            }
        }

        private bool waitingForJoystickHoldingCheck;

        private bool holdingJoystick;

        private bool HoldingJoystick
        {
            set
            {
                if (holdingJoystick != value)
                {
                    holdingJoystick = value;

                    centeringSettled = false;

                    if (!value) { target.SetProgramVariable(variableName, Vector4.zero); }
                }
            }
            get => holdingJoystick;
        }

        private bool centeringSettled;

        private VRCPlayerApi localPlayer;

        private void Start()
        {
            rightHanded = handMode == VRC_Pickup.PickupHand.Right;

            string controllerType = rightHanded ? "Secondary" : "Primary";

            thumbstickHorizontalAxis = string.Format("Oculus_CrossPlatform_{0}ThumbstickHorizontal", controllerType);
            thumbstickVerticalAxis = string.Format("Oculus_CrossPlatform_{0}ThumbstickVertical", controllerType);

            if(!(localPlayer = Networking.LocalPlayer).IsUserInVR()) { Destroy(this); }
        }

        private void LateUpdate()
        {
            float x, y, z, w;

            if (waitingForJoystickHoldingCheck)
            {
                VRCPlayerApi.TrackingData td;

                if (rightHanded)
                {
                    td = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
                }
                else
                {
                    td = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
                }

                handPos = td.position;

                handRot = td.rotation;

                if (GetHandDistanceToJoystick() < 0.1f)
                {
                    holdingJoystick = true;

                    joystickRotationOffset = Quaternion.Inverse(handRot) * joystickRoot.rotation;
                }

                waitingForJoystickHoldingCheck = false;
            }

            if (holdingJoystick)
            {
                VRCPlayerApi.TrackingData td;

                if (rightHanded)
                {
                    td = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
                }
                else
                {
                    td = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
                }

                handRot = td.rotation;

                joystickRoot.rotation = handRot * joystickRotationOffset;
                Vector3 localEuler = joystickRoot.localEulerAngles;
                joystickRoot.localEulerAngles = new Vector3(Mathf.Clamp(Mathf.DeltaAngle(0f, localEuler.x), -range.x, range.x), 0f, Mathf.Clamp(Mathf.DeltaAngle(0f, localEuler.z), -range.y, range.y));

                z = Input.GetAxisRaw(thumbstickHorizontalAxis);
                w = Input.GetAxisRaw(thumbstickVerticalAxis);

                float jx = joystickRoot.localEulerAngles.x;

                y = Mathf.Clamp(Mathf.DeltaAngle(0f, -jx) / range.x, -1f, 1f);

                float jz = joystickRoot.localEulerAngles.z;

                x = Mathf.Clamp(Mathf.DeltaAngle(0f, -jz) / range.y, -1f, 1f);

                target.SetProgramVariable(variableName, new Vector4(Mathf.Pow(Mathf.Abs(x), inputExponent) * Mathf.Sign(x), Mathf.Pow(Mathf.Abs(y), inputExponent) * Mathf.Sign(y), z, w));

                float amplitude = Mathf.Max(Mathf.Abs(x) / range.x, Mathf.Abs(y) / range.x);

                localPlayer.PlayHapticEventInHand(handMode, 0.5f, amplitude, amplitude);
            }
            else if (centeringSettled == false)
            {
                Quaternion rotationCurrent = joystickRoot.rotation;
                Quaternion rotationTarget = transform.rotation;

                if (Quaternion.Angle(rotationCurrent, rotationTarget) < float.Epsilon) { centeringSettled = true; }

                joystickRoot.rotation = Quaternion.RotateTowards(rotationCurrent, rotationTarget, Time.deltaTime * Quaternion.Angle(rotationCurrent, rotationTarget) * 5f);
            }
        }

        private float GetHandDistanceToJoystick()
        {
            return Vector3.Distance(handPos, joystickGrabPoint.position);
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

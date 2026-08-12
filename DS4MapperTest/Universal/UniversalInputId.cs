namespace DS4MapperTest.Universal
{
    // Universal IDs use neutral position or semantic names only. Controller
    // labels such as Cross, ZR or L4 belong in controller display metadata.
    public enum UniversalInputId : ushort
    {
        FaceButtonNorth = 1,
        FaceButtonEast,
        FaceButtonSouth,
        FaceButtonWest,

        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        LeftShoulder,
        RightShoulder,
        LeftTrigger,
        RightTrigger,
        LeftTriggerFullPull,
        RightTriggerFullPull,

        LeftStick,
        RightStick,
        LeftStickClick,
        RightStickClick,
        LeftStickTouch,
        RightStickTouch,

        Menu,
        View,
        System,
        NavigationPrimary,
        NavigationSecondary,
        Capture,
        Mute,
        QuickAccessMenu,

        LeftRearPrimary,
        RightRearPrimary,
        LeftRearSecondary,
        RightRearSecondary,
        LeftRearTertiary,
        RightRearTertiary,
        LeftGripTouch,
        RightGripTouch,

        LeftSidePrimary,
        LeftSideSecondary,
        RightSidePrimary,
        RightSideSecondary,

        PrimaryTouchSurface,
        LeftTouchSurface,
        RightTouchSurface,
        PrimaryTouchSurfaceClick,
        LeftTouchSurfaceClick,
        RightTouchSurfaceClick,
        PrimaryTouchContact,
        LeftTouchContact,
        RightTouchContact,

        Gyroscope,
        Accelerometer,

        MiscButton1,
        MiscButton2,
        MiscButton3,
        MiscButton4,
        MiscButton5,
        MiscButton6,
        MiscButton7,
        MiscButton8,
        MiscButton9,
        MiscButton10,
        MiscButton11,
        MiscButton12,
        MiscButton13,
        MiscButton14,
        MiscButton15,
        MiscButton16,

        MiscAxis1,
        MiscAxis2,
        MiscAxis3,
        MiscAxis4,
        MiscAxis5,
        MiscAxis6,
        MiscAxis7,
        MiscAxis8,

        MiscTouchSurface1,
        MiscTouchSurface2,
    }
}

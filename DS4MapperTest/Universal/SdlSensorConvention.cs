namespace DS4MapperTest.Universal
{
    /// <summary>
    /// Converts SDL's sensor frame into the one a <see cref="GyroEventFrame"/>
    /// is defined in.
    ///
    /// SDL normalises every controller it supports into one documented frame
    /// (SDL_SensorType): the axes are +X right, +Y top, +Z closer to the
    /// player, and a rotation is positive when an observer out along that axis
    /// sees it turning counter-clockwise. So SDL reports a positive gyro X
    /// while the controller is being pitched up, a positive gyro Y while it is
    /// being turned left, and a positive gyro Z while it is being rolled
    /// anti-clockwise.
    ///
    /// The mapper's frame is the opposite sense on all three. Gyro mouse feeds
    /// yaw straight to mouse X and pitch straight to mouse Y, and mouse Y grows
    /// downwards, so aiming right has to produce a positive yaw and aiming up a
    /// negative pitch. Roll follows the same sense as yaw so that choosing it
    /// as the horizontal source turns the same way round.
    ///
    /// The accelerometer differs by the frame it is expressed in rather than by
    /// sense: the mapper's X and Y point left and down against SDL's right and
    /// up, while Z agrees. That is the DualShock convention the readers
    /// produce, which is why an SDL-sourced controller reports its gyro frame
    /// as <see cref="InputDeviceType.DS4"/> regardless of what hardware it
    /// actually is (see UniversalMapper.GyroSensorConventionDeviceType).
    /// </summary>
    public static class SdlSensorConvention
    {
        // The device family whose sensor convention an SDL-sourced gyro frame
        // is expressed in once converted.
        public const InputDeviceType FrameDeviceType = InputDeviceType.DS4;

        public static double GyroPitchToFrame(double sdlGyroX) => -sdlGyroX;
        public static double GyroYawToFrame(double sdlGyroY) => -sdlGyroY;
        public static double GyroRollToFrame(double sdlGyroZ) => -sdlGyroZ;

        public static double AccelXToFrame(double sdlAccelX) => -sdlAccelX;
        public static double AccelYToFrame(double sdlAccelY) => -sdlAccelY;
        public static double AccelZToFrame(double sdlAccelZ) => sdlAccelZ;
    }
}

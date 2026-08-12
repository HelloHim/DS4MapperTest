using System;
using System.Globalization;
using System.Windows.Data;

namespace DS4MapperTest.Converters
{
    /// <summary>
    /// Renders a <see cref="ProfileEntity"/> as its display name from a binding
    /// with no property path.
    ///
    /// A path-based binding (<c>{Binding Name}</c>) makes WPF resolve and cache a
    /// reflection accessor for ProfileEntity.Name, and -- because ProfileEntity
    /// raises a NameChanged event -- also subscribe to it through a
    /// PropertyDescriptor. Both of those are invoked with whatever object the
    /// binding currently sees, including the internal sentinels WPF substitutes
    /// while an ItemsControl's selection is being torn down
    /// (DependencyProperty.UnsetValue and the disconnected-item marker, both
    /// MS.Internal.NamedObject). Reflection then throws
    /// "Object type DS4MapperTest.ProfileEntity does not match target type
    /// MS.Internal.NamedObject", which escapes the binding engine and aborts
    /// whatever profile operation was running.
    ///
    /// A path-less binding never reflects over the item, so the sentinel simply
    /// falls through to string.Empty here instead of throwing.
    /// </summary>
    public class ProfileEntityNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as ProfileEntity)?.Name ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

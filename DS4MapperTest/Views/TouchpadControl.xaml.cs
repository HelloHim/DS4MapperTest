using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.Views.TouchpadActionPropControls;

namespace DS4MapperTest.Views
{
    public partial class TouchpadControl : UserControl
    {
        private enum TouchpadPadSide
        {
            Whole,
            Left,
            Right,
            Other,
        }

        private readonly List<TabItem> touchpadSideTabs = new List<TabItem>();
        private ObservableCollection<TouchBindingItemsTest> observedTouchpadBindings;

        public TouchpadControl()
        {
            InitializeComponent();

            Loaded += (sender, e) =>
            {
                HookTouchpadBindings();
                BuildTouchpadSideTabs();
            };
            DataContextChanged += (sender, e) =>
            {
                HookTouchpadBindings();
                BuildTouchpadSideTabs();
            };
        }

        private void HookTouchpadBindings()
        {
            if (observedTouchpadBindings != null)
            {
                observedTouchpadBindings.CollectionChanged -= TouchpadBindings_CollectionChanged;
            }

            observedTouchpadBindings = (DataContext as ProfileEditorTestViewModel)?.TouchpadBindings;

            if (observedTouchpadBindings != null)
            {
                observedTouchpadBindings.CollectionChanged += TouchpadBindings_CollectionChanged;
            }
        }

        private void TouchpadBindings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            BuildTouchpadSideTabs();
        }

        private void BuildTouchpadSideTabs()
        {
            foreach (TabItem tab in touchpadSideTabs)
            {
                TabControlRoot.Items.Remove(tab);
            }

            touchpadSideTabs.Clear();

            if (DataContext is not ProfileEditorTestViewModel vm)
            {
                return;
            }

            if (vm.TouchpadBindings.Count == 0)
            {
                TabItem emptyTab = new TabItem
                {
                    Header = "Touchpad Settings",
                    Content = CreateMessage("No supported touchpad binding inputs are available for this controller."),
                };
                touchpadSideTabs.Add(emptyTab);
                TabControlRoot.Items.Add(emptyTab);
                return;
            }

            var ordered = vm.TouchpadBindings
                .Select((item, index) => (item, classification: ClassifyTouchpadBinding(item.BindingName), index))
                .OrderBy(entry => entry.classification.rank)
                .ThenBy(entry => entry.index);

            foreach (var entry in ordered)
            {
                FrameworkElement content = BuildSideSettingsContent(entry.item, entry.classification.side, vm);
                TabItem tab = new TabItem
                {
                    Header = GetTouchpadTabHeader(entry.item, entry.classification.side),
                    Content = content,
                };
                touchpadSideTabs.Add(tab);
                TabControlRoot.Items.Add(tab);
            }
        }

        private FrameworkElement BuildSideSettingsContent(TouchBindingItemsTest item, TouchpadPadSide side,
            ProfileEditorTestViewModel vm)
        {
            DataTemplate template = (DataTemplate)Resources["TouchpadPadSettingsTemplate"];
            FrameworkElement content = (FrameworkElement)template.LoadContent();
            content.DataContext = item;

            bool showSteamRotation = (side == TouchpadPadSide.Left || side == TouchpadPadSide.Right) &&
                vm.HasSteamPadRotation;

            if (showSteamRotation && content.FindName("SteamPadRotationSlot") is StackPanel slot)
            {
                string rotationTemplateKey = side == TouchpadPadSide.Left
                    ? "LeftPadRotationRowTemplate" : "RightPadRotationRowTemplate";
                DataTemplate rotationTemplate = (DataTemplate)Resources[rotationTemplateKey];
                FrameworkElement rotationRow = (FrameworkElement)rotationTemplate.LoadContent();
                rotationRow.DataContext = vm.SteamPadRotation;
                slot.Children.Add(rotationRow);

                if (content.FindName("ModeSettingsSectionRoot") is Border modeSettingsSection)
                {
                    BindingOperations.ClearBinding(modeSettingsSection, UIElement.VisibilityProperty);
                    modeSettingsSection.Visibility = Visibility.Visible;
                }
            }

            return content;
        }

        private static (int rank, TouchpadPadSide side) ClassifyTouchpadBinding(string bindingName)
        {
            return bindingName switch
            {
                "Touchpad" => (0, TouchpadPadSide.Whole),
                "TouchpadLeft" or "LeftTouchpad" => (1, TouchpadPadSide.Left),
                "TouchpadRight" or "RightTouchpad" => (2, TouchpadPadSide.Right),
                _ => (3, TouchpadPadSide.Other),
            };
        }

        private static string GetTouchpadTabHeader(TouchBindingItemsTest item, TouchpadPadSide side)
        {
            return side switch
            {
                TouchpadPadSide.Whole => "Touchpad Settings",
                TouchpadPadSide.Left => "Left Touchpad Settings",
                TouchpadPadSide.Right => "Right Touchpad Settings",
                _ => $"{item.DisplayName} Settings",
            };
        }

        private void InlineSettingsHost_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ContentControl host ||
                host.DataContext is not TouchBindingItemsTest item)
            {
                return;
            }

            if (DataContext is ProfileEditorTestViewModel owner)
            {
                owner.PopulateMapperEditActionRefs(item.Mapper);
            }

            if (host.Tag is not string section)
            {
                section = "Bindings";
            }

            RenderInlineHost(host, item, section);

            item.PropertyChanged -= TouchpadItem_PropertyChanged;
            item.PropertyChanged += TouchpadItem_PropertyChanged;
        }

        private void TouchpadItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TouchBindingItemsTest.MappedAction) &&
                e.PropertyName != nameof(TouchBindingItemsTest.SelectedActionIndex))
            {
                return;
            }

            RefreshInlineHosts(this, sender as TouchBindingItemsTest);
        }

        private void RefreshInlineHosts(DependencyObject root, TouchBindingItemsTest item)
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ContentControl host &&
                    host.DataContext == item &&
                    host.Tag is string section)
                {
                    RenderInlineHost(host, item, section);
                }

                RefreshInlineHosts(child, item);
            }
        }

        private void RenderInlineHost(ContentControl host, TouchBindingItemsTest item, string section)
        {
            host.Content = section switch
            {
                "Bindings" => CreateBindingContent(host, item),
                "ModeSettings" => CreateSectionContent(host, item, section),
                "MouseMovement" => CreateSectionContent(host, item, section),
                "SensitivityCalibration" => CreateSectionContent(host, item, section),
                "FilteringStabilisation" => CreateSectionContent(host, item, section),
                "ZonesGestures" => CreateSectionContent(host, item, section),
                "TrackballScroll" => CreateSectionContent(host, item, section),
                "Advanced" => CreateSectionContent(host, item, section),
                "Extra" => CreateSectionContent(host, item, section),
                _ => null,
            };
        }

        private object CreateBindingContent(ContentControl host, TouchBindingItemsTest item)
        {
            if (item.MappedAction is TouchpadSingleButton)
            {
                return CreateActionControl(host, item, null);
            }

            return null;
        }

        private object CreateSectionContent(ContentControl host, TouchBindingItemsTest item, string section)
        {
            if (item.IsUnbound)
            {
                return CreateMessage(GetUnboundMessage(item, section));
            }

            bool hasSettings = section switch
            {
                "MouseMovement" => item.IsMouseMovementAction,
                "ModeSettings" => item.IsModeSettingsAction,
                "SensitivityCalibration" => item.IsSensitivityCalibrationAction,
                "FilteringStabilisation" => item.IsFilteringStabilisationAction,
                "ZonesGestures" => item.IsZoneGestureAction,
                "TrackballScroll" => item.IsTrackballScrollAction,
                "Advanced" => item.IsAdvancedAction,
                "Extra" => item.IsExtraAction,
                _ => false,
            };

            if (!hasSettings)
            {
                return CreateMessage(GetNoSettingsMessage(item, section));
            }

            return CreateActionControl(host, item, section);
        }

        private FrameworkElement CreateActionControl(ContentControl host, TouchBindingItemsTest item, string section)
        {
            FrameworkElement propControlElement = CreateActionControlCore(host, item);

            if (propControlElement is ISectionAwareTouchpadPropControl sectionAware &&
                TryParseSection(section, out TouchpadSettingsSection parsedSection))
            {
                sectionAware.ApplySection(parsedSection);
            }

            return propControlElement;
        }

        private static bool TryParseSection(string section, out TouchpadSettingsSection parsedSection)
        {
            switch (section)
            {
                case "MouseMovement":
                    parsedSection = TouchpadSettingsSection.MouseMovement;
                    return true;
                case "ModeSettings":
                    parsedSection = TouchpadSettingsSection.ModeSettings;
                    return true;
                case "SensitivityCalibration":
                    parsedSection = TouchpadSettingsSection.SensitivityCalibration;
                    return true;
                case "FilteringStabilisation":
                    parsedSection = TouchpadSettingsSection.FilteringStabilisation;
                    return true;
                case "ZonesGestures":
                    parsedSection = TouchpadSettingsSection.ZonesGestures;
                    return true;
                case "TrackballScroll":
                    parsedSection = TouchpadSettingsSection.TrackballScroll;
                    return true;
                case "Advanced":
                    parsedSection = TouchpadSettingsSection.Advanced;
                    return true;
                case "Extra":
                    parsedSection = TouchpadSettingsSection.Extra;
                    return true;
                default:
                    parsedSection = default;
                    return false;
            }
        }

        private FrameworkElement CreateActionControlCore(ContentControl host, TouchBindingItemsTest item)
        {
            switch (item.MappedAction)
            {
                case TouchpadStickAction:
                    {
                        TouchpadStickActionPropControl propControl = new TouchpadStickActionPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case TouchpadActionPad:
                    {
                        TouchpadActionPadPropControl propControl = new TouchpadActionPadPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case TouchpadMouseJoystick:
                    {
                        TouchpadMouseJoystickPropControl propControl = new TouchpadMouseJoystickPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        return propControl;
                    }
                case TouchpadMouse:
                    {
                        TouchpadMousePropControl propControl = new TouchpadMousePropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        return propControl;
                    }
                case TouchpadAbsAction:
                    {
                        TouchpadAbsMousePropControl propControl = new TouchpadAbsMousePropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.ActionBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                null);
                        return propControl;
                    }
                case TouchpadCircular:
                    {
                        TouchpadCircularPropControl propControl = new TouchpadCircularPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case TouchpadSingleButton:
                    {
                        TouchpadSingleButtonPropControl propControl = new TouchpadSingleButtonPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case TouchpadDirectionalSwipe:
                    {
                        TouchpadDirSwipePropControl propControl = new TouchpadDirSwipePropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        propControl.RequestFuncEditor += (sender, args) =>
                            ShowFuncEditor(host, propControl, args.DirBtn, args.RealAction,
                                (oldAction, newAction) => args.UpdateActHandler?.Invoke(oldAction, newAction),
                                propControl.RefreshView);
                        return propControl;
                    }
                case TouchpadFlickStick:
                    {
                        TouchpadFlickStickPropControl propControl = new TouchpadFlickStickPropControl();
                        propControl.PostInit(item.Mapper, item.MappedAction);
                        return propControl;
                    }
                default:
                    return CreateMessage($"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no inline settings.");
            }
        }

        private void ShowFuncEditor(ContentControl host, UserControl baseControl, ButtonAction action,
            bool realAction, Action<ButtonAction, ButtonAction> updateHandler,
            Action refreshBaseControl)
        {
            FuncBindingControl funcControl = new FuncBindingControl();
            funcControl.PostInit((host.DataContext as TouchBindingItemsTest).Mapper, action);
            funcControl.FuncBindVM.IsRealAction = realAction;
            funcControl.PreActionSwitch += (oldAction, newAction) => updateHandler?.Invoke(oldAction, newAction);
            funcControl.ActionChanged += (sender, newAction) => updateHandler?.Invoke(null, newAction);
            funcControl.RequestBindingEditor += (sender, func) => ShowOutputEditor(host, funcControl, action, func);
            funcControl.RequestClose += (sender, args) =>
            {
                refreshBaseControl?.Invoke();
                host.Content = baseControl;
            };

            host.Content = funcControl;
        }

        private void ShowOutputEditor(ContentControl host, FuncBindingControl funcControl,
            ButtonAction action, ActionFunc func)
        {
            OutputBindingEditorControl outputControl = new OutputBindingEditorControl();
            outputControl.PostInit((host.DataContext as TouchBindingItemsTest).Mapper, action, func);
            outputControl.Finished += (sender, args) =>
            {
                funcControl.RefreshView();
                host.Content = funcControl;
            };

            host.Content = outputControl;
        }

        private void TouchpadClickAddExtraBindingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void TouchpadClickAddExtraBindingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem ||
                menuItem.Tag is not string tag ||
                menuItem.Parent is not ContextMenu menu ||
                menu.PlacementTarget is not FrameworkElement target ||
                target.DataContext is not FaceButtonBindingItem buttonItem)
            {
                return;
            }

            FaceBindingFuncKind? kind = tag switch
            {
                "Hold" => FaceBindingFuncKind.Hold,
                "Double" => FaceBindingFuncKind.Double,
                "Distance" => FaceBindingFuncKind.Distance,
                "Chorded" => FaceBindingFuncKind.Chorded,
                "Start" => FaceBindingFuncKind.Start,
                "Release" => FaceBindingFuncKind.Release,
                _ => null,
            };

            if (kind == null) return;

            buttonItem.AddExtraBinding(kind.Value);
        }

        private void TouchpadClickRemoveBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FaceButtonFuncItem item })
            {
                item.Owner.RemoveBinding(item);
            }
        }

        private TextBlock CreateMessage(string message)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            };

            if (TryFindResource("JsmccBodyText") is Style style)
            {
                textBlock.Style = style;
            }

            return textBlock;
        }

        private static string GetUnboundMessage(TouchBindingItemsTest item, string section)
        {
            return section switch
            {
                "MouseMovement" => $"{item.DisplayName} is currently set to Unbound. Choose a movement mode in Mode to configure movement settings.",
                "ModeSettings" => $"{item.DisplayName} is currently set to Unbound. Choose D-Pad Zones in Mode to configure touchpad mode settings.",
                "SensitivityCalibration" => $"{item.DisplayName} is currently set to Unbound. Choose a touchpad mode in Mode to configure sensitivity and calibration settings.",
                "FilteringStabilisation" => $"{item.DisplayName} is currently set to Unbound. Choose a touchpad mode in Mode to configure filtering and stabilisation settings.",
                "ZonesGestures" => $"{item.DisplayName} is currently set to Unbound. Choose a zone or gesture-capable mode in Mode to configure gesture settings.",
                "TrackballScroll" => $"{item.DisplayName} is currently set to Unbound. Choose a trackball or scroll mode in Mode to configure these settings.",
                "Advanced" => $"{item.DisplayName} is currently set to Unbound. Choose a touchpad mode in Mode to configure advanced settings.",
                _ => $"{item.DisplayName} is currently set to Unbound.",
            };
        }

        private static string GetNoSettingsMessage(TouchBindingItemsTest item, string section)
        {
            return section switch
            {
                "MouseMovement" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no movement settings.",
                "ModeSettings" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no dedicated mode settings.",
                "SensitivityCalibration" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no sensitivity and calibration settings.",
                "FilteringStabilisation" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no filtering and stabilisation settings.",
                "ZonesGestures" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no zone or gesture settings.",
                "TrackballScroll" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no trackball or scroll settings.",
                "Advanced" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no advanced settings.",
                _ => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no settings for this subsection.",
            };
        }
    }
}

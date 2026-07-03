using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.ViewModels;
using DS4MapperTest.Views.TouchpadActionPropControls;

namespace DS4MapperTest.Views
{
    public partial class TouchpadControl : UserControl
    {
        public TouchpadControl()
        {
            InitializeComponent();
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
                "MouseMovement" => CreateSectionContent(host, item, section),
                "ZonesGestures" => CreateSectionContent(host, item, section),
                "TrackballScroll" => CreateSectionContent(host, item, section),
                "Advanced" => CreateSectionContent(host, item, section),
                _ => null,
            };
        }

        private object CreateBindingContent(ContentControl host, TouchBindingItemsTest item)
        {
            if (item.MappedAction is TouchpadSingleButton)
            {
                return CreateActionControl(host, item);
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
                "ZonesGestures" => item.IsZoneGestureAction,
                "TrackballScroll" => item.IsTrackballScrollAction,
                "Advanced" => item.IsAdvancedAction,
                _ => false,
            };

            if (!hasSettings)
            {
                return CreateMessage(GetNoSettingsMessage(item, section));
            }

            return CreateActionControl(host, item);
        }

        private FrameworkElement CreateActionControl(ContentControl host, TouchBindingItemsTest item)
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
                "MouseMovement" => $"{item.DisplayName} is currently set to Unbound. Choose a movement mode in Bindings to configure movement settings.",
                "ZonesGestures" => $"{item.DisplayName} is currently set to Unbound. Choose a zone or gesture-capable mode in Bindings to configure gesture settings.",
                "TrackballScroll" => $"{item.DisplayName} is currently set to Unbound. Choose a trackball or scroll mode in Bindings to configure these settings.",
                "Advanced" => $"{item.DisplayName} is currently set to Unbound. Choose a touchpad mode in Bindings to configure advanced settings.",
                _ => $"{item.DisplayName} is currently set to Unbound.",
            };
        }

        private static string GetNoSettingsMessage(TouchBindingItemsTest item, string section)
        {
            return section switch
            {
                "MouseMovement" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no movement settings.",
                "ZonesGestures" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no zone or gesture settings.",
                "TrackballScroll" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no trackball or scroll settings.",
                "Advanced" => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no advanced settings.",
                _ => $"{item.DisplayName} is set to {item.ActionDisplayName}. This mode has no settings for this subsection.",
            };
        }
    }
}

using System.Diagnostics;
using DS4MapperTest.TriggerActions;

namespace DS4MapperTest.MapperUtil
{
    // Mutable per-instance state consumed by DualStageEvaluator.ProcessCurrentStage.
    // Extracted from TriggerDualStageAction so the same state machine can drive both
    // trigger dual-stage pull and touchpad dual-stage pressure evaluation.
    public class DualStageEvaluatorState
    {
        public bool startCheck;
        public Stopwatch checkTimeWatch = new Stopwatch();
        public bool outputActive;
        public TriggerDualStageAction.EngageButtonsMode actionStateMode =
            TriggerDualStageAction.EngageButtonsMode.Both;

        public void StartStageProcessing(bool useTime = true)
        {
            startCheck = true;
            if (useTime)
            {
                checkTimeWatch.Restart();
            }

            outputActive = false;
            actionStateMode = TriggerDualStageAction.EngageButtonsMode.None;
        }

        public void ResetStageState()
        {
            startCheck = false;
            if (checkTimeWatch.IsRunning)
            {
                checkTimeWatch.Reset();
            }

            outputActive = false;
            actionStateMode = TriggerDualStageAction.EngageButtonsMode.None;
        }
    }

    public static class DualStageEvaluator
    {
        // Body moved verbatim from TriggerDualStageAction.ProcessCurrentStage. The only
        // change is that all mutable state formerly held on "this" is now read/written on
        // "state", and the HairTrigger branch's read of the previous frame's full-pull-active
        // flag is passed in explicitly as previousFullPullActive.
        public static TriggerDualStageAction.ActiveZoneButtons ProcessCurrentStage(
            TriggerDualStageAction.DualStageMode mode,
            double axisNorm,
            bool fullPullClick,
            bool forceHipTime,
            int hipFireMs,
            bool previousFullPullActive,
            DualStageEvaluatorState state)
        {
            TriggerDualStageAction.ActiveZoneButtons result = TriggerDualStageAction.ActiveZoneButtons.None;

            switch (mode)
            {
                case TriggerDualStageAction.DualStageMode.Threshold:
                    {
                        if (fullPullClick)
                        {
                            result = TriggerDualStageAction.ActiveZoneButtons.SoftPull |
                                TriggerDualStageAction.ActiveZoneButtons.FullPull;
                        }
                        else if (axisNorm != 0.0)
                        {
                            result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                        }
                        else
                        {
                            result = TriggerDualStageAction.ActiveZoneButtons.None;
                        }
                    }

                    break;
                case TriggerDualStageAction.DualStageMode.ExclusiveButtons:
                    {
                        if (fullPullClick)
                        {
                            state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.FullPullOnly;
                            result = TriggerDualStageAction.ActiveZoneButtons.FullPull;
                        }
                        else if (axisNorm != 0.0 &&
                            state.actionStateMode != TriggerDualStageAction.EngageButtonsMode.FullPullOnly)
                        {
                            state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.Both;
                            result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                        }
                        else if (axisNorm == 0.0)
                        {
                            state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.None;
                            result = TriggerDualStageAction.ActiveZoneButtons.None;
                        }
                    }

                    break;
                case TriggerDualStageAction.DualStageMode.HairTrigger:
                    {
                        if (fullPullClick)
                        {
                            // Full pull now activates both. Soft pull action
                            // no longer engaged with threshold
                            result = TriggerDualStageAction.ActiveZoneButtons.SoftPull |
                                TriggerDualStageAction.ActiveZoneButtons.FullPull;
                        }
                        else if (axisNorm != 0.0 && previousFullPullActive)
                        {
                            // Full pull not engaged yet. Activate Soft pull action.
                            result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                        }
                        else if (axisNorm == 0.0 && state.outputActive)
                        {
                            state.ResetStageState();
                        }
                    }

                    break;
                case TriggerDualStageAction.DualStageMode.HipFire:
                    {
                        if (axisNorm != 0.0 && !state.startCheck)
                        {
                            state.StartStageProcessing();
                        }
                        else if (axisNorm != 0.0 && !state.outputActive)
                        {
                            // Consider action active depending on timer
                            // or whether full pull is achieved
                            bool nowActive = (!forceHipTime && fullPullClick) ||
                                state.checkTimeWatch.ElapsedMilliseconds > hipFireMs;

                            if (nowActive)
                            {
                                state.checkTimeWatch.Stop();
                                state.outputActive = nowActive;

                                if (fullPullClick)
                                {
                                    state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.FullPullOnly;
                                }
                                else if (axisNorm != 0.0)
                                {
                                    state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.Both;
                                }
                            }
                        }
                        else if (state.outputActive)
                        {
                            if (fullPullClick)
                            {
                                result = TriggerDualStageAction.ActiveZoneButtons.FullPull;

                                if (state.actionStateMode == TriggerDualStageAction.EngageButtonsMode.Both)
                                {
                                    result = result | TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                                }
                            }
                            else if (axisNorm != 0.0 &&
                                state.actionStateMode == TriggerDualStageAction.EngageButtonsMode.Both)
                            {
                                result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                            }
                            else if (axisNorm == 0.0)
                            {
                                state.ResetStageState();
                            }
                        }
                        else if (state.startCheck)
                        {
                            state.ResetStageState();
                        }
                    }

                    break;
                case TriggerDualStageAction.DualStageMode.HipFireExclusiveButtons:
                    {
                        if (axisNorm == 0.0)
                        {
                            if (state.startCheck)
                            {
                                state.ResetStageState();
                            }

                            state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.None;
                            result = TriggerDualStageAction.ActiveZoneButtons.None;
                        }
                        else if (axisNorm != 0.0 && !state.startCheck)
                        {
                            state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.None;

                            if (!forceHipTime && fullPullClick)
                            {
                                state.StartStageProcessing(false);
                            }
                            else if (axisNorm != 0.0)
                            {
                                state.StartStageProcessing();
                            }
                        }

                        if (axisNorm != 0.0)
                        {
                            if (state.startCheck && !state.outputActive)
                            {
                                // Consider action active depending on timer
                                // or whether full pull is achieved
                                bool nowActive = (!forceHipTime && fullPullClick) ||
                                    state.checkTimeWatch.ElapsedMilliseconds > hipFireMs;

                                if (nowActive)
                                {
                                    if (state.checkTimeWatch.IsRunning)
                                    {
                                        state.checkTimeWatch.Stop();
                                    }

                                    state.outputActive = nowActive;

                                    if (fullPullClick)
                                    {
                                        state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.FullPullOnly;
                                        result = TriggerDualStageAction.ActiveZoneButtons.FullPull;
                                    }
                                    else if (axisNorm != 0.0)
                                    {
                                        state.actionStateMode = TriggerDualStageAction.EngageButtonsMode.SoftPullOnly;
                                        result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                                    }
                                }
                            }
                            else if (state.startCheck && state.outputActive)
                            {
                                if (fullPullClick &&
                                    state.actionStateMode == TriggerDualStageAction.EngageButtonsMode.FullPullOnly)
                                {
                                    result = TriggerDualStageAction.ActiveZoneButtons.FullPull;
                                }
                                else if (axisNorm != 0.0 &&
                                    state.actionStateMode == TriggerDualStageAction.EngageButtonsMode.SoftPullOnly)
                                {
                                    result = TriggerDualStageAction.ActiveZoneButtons.SoftPull;
                                }
                            }
                        }
                    }

                    break;
                default:
                    break;
            }

            return result;
        }
    }
}

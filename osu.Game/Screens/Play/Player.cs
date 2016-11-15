﻿//Copyright (c) 2007-2016 ppy Pty Ltd <contact@ppy.sh>.
//Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Modes;
using osu.Game.Modes.Objects;
using osu.Game.Modes.Objects.Drawables;
using osu.Game.Modes.UI;
using osu.Game.Screens.Backgrounds;
using OpenTK.Input;
using MouseState = osu.Framework.Input.MouseState;

namespace osu.Game.Screens.Play
{
    public class Player : OsuGameMode
    {
        const bool autoplay = false;

        protected override BackgroundMode CreateBackground() => new BackgroundModeCustom(@"Backgrounds/bg4");

        internal override bool ShowOverlays => false;

        public BeatmapInfo BeatmapInfo;

        PlayerInputManager inputManager;

        class PlayerInputManager : UserInputManager
        {
            public PlayerInputManager(BasicGameHost host)
                : base(host)
            {
            }

            protected override void UpdateMouseState(InputState state)
            {
                base.UpdateMouseState(state);

                MouseState mouse = (MouseState)state.Mouse;

                foreach (Key k in state.Keyboard.Keys)
                {
                    switch (k)
                    {
                        case Key.Z:
                            mouse.ButtonStates.Find(s => s.Button == MouseButton.Left).State = true;
                            break;
                        case Key.X:
                            mouse.ButtonStates.Find(s => s.Button == MouseButton.Right).State = true;
                            break;
                    }
                }
                
            }
        }


        public PlayMode PreferredPlayMode;

        protected override IFrameBasedClock Clock => playerClock;

        private InterpolatingFramedClock playerClock;
        private IAdjustableClock sourceClock;
        private Ruleset Ruleset;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, BeatmapDatabase beatmaps, OsuGameBase game)
        {
            try
            {
                if (Beatmap == null)
                    Beatmap = beatmaps.GetWorkingBeatmap(BeatmapInfo);
            }
            catch
            {
                //couldn't load, hard abort!
                Exit();
                return;
            }

            AudioTrack track = Beatmap.Track;

            if (track != null)
            {
                audio.Track.SetExclusive(track);
                sourceClock = track;
            }

            sourceClock = (IAdjustableClock)track ?? new StopwatchClock();
            playerClock = new InterpolatingFramedClock(sourceClock);

            Schedule(() =>
            {
                sourceClock.Reset();
                sourceClock.Start();
            });

            HitRenderer hitRenderer;
            ScoreOverlay scoreOverlay;

            var beatmap = Beatmap.Beatmap;

            if (beatmap.BeatmapInfo?.Mode > PlayMode.Osu)
            {
                //we only support osu! mode for now because the hitobject parsing is crappy and needs a refactor.
                Exit();
                return;
            }

            PlayMode usablePlayMode = beatmap.BeatmapInfo?.Mode > PlayMode.Osu ? beatmap.BeatmapInfo.Mode : PreferredPlayMode;

            Ruleset = Ruleset.GetRuleset(usablePlayMode);

            scoreOverlay = Ruleset.CreateScoreOverlay();

            hitRenderer = Ruleset.CreateHitRendererWith(beatmap.HitObjects);

            hitRenderer.OnHit += delegate (HitObject h) { scoreOverlay.OnHit(h); };
            hitRenderer.OnMiss += delegate (HitObject h) { scoreOverlay.OnMiss(h); };

            if (autoplay)
                hitRenderer.Schedule(() => hitRenderer.DrawableObjects.ForEach(h => h.State = ArmedState.Armed));

            Children = new Drawable[]
            {
                inputManager = new PlayerInputManager(game.Host)
                {
                    PassThrough = false,
                    Children = new Drawable[]
                    {
                        hitRenderer,
                    }
                },
                scoreOverlay,
            };
        }

        protected override void Update()
        {
            base.Update();
            playerClock.ProcessFrame();
        }
    }
}
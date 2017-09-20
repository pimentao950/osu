// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Lists;
using osu.Framework.MathUtils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;

namespace osu.Game.Tests.Visual
{
    internal class TestCaseEditorTimingTimeline : OsuTestCase
    {
        public TestCaseEditorTimingTimeline()
        {
            Add(new TimingTimeline
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(500, 50)
            });
        }

        private class TimingTimeline : CompositeDrawable
        {
            private const float corner_radius = 5;
            private const float contents_padding = 15;
            private const float marker_bar_width = 2;

            private readonly Drawable background;

            private readonly Container markerContainer;

            private readonly Drawable timelineBar;
            private readonly Drawable marker;

            private readonly Bindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

            public TimingTimeline()
            {
                Masking = true;
                CornerRadius = 5;

                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = contents_padding, Right = contents_padding },
                        Children = new Drawable[]
                        {
                            markerContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = marker = new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.Centre,
                                    RelativePositionAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    AutoSizeAxes = Axes.X,
                                    Children = new Drawable[]
                                    {
                                        new Triangle
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.BottomCentre,
                                            Scale = new Vector2(1, -1),
                                            Size = new Vector2(10, 5),
                                        },
                                        new Triangle
                                        {
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                            Size = new Vector2(10, 5)
                                        },
                                        new Box
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            RelativeSizeAxes = Axes.Y,
                                            Width = 2,
                                            EdgeSmoothness = new Vector2(1, 0)
                                        }
                                    }
                                }
                            },
                            new ControlPointTimeline
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.35f
                            },
                            new BookmarkTimeline
                            {
                                Anchor = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.35f
                            },
                            timelineBar = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Circle
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreRight,
                                        Size = new Vector2(5)
                                    },
                                    new Box
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Height = 1,
                                        EdgeSmoothness = new Vector2(0, 1),
                                    },
                                    new Circle
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(5)
                                    },
                                }
                            },
                            new BreakTimeline
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.25f
                            }
                        }
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuGameBase osuGame, OsuColour colours)
            {
                background.Colour = colours.Gray1;
                marker.Colour = colours.Red;
                timelineBar.Colour = colours.Gray5;

                beatmap.BindTo(osuGame.Beatmap);

                markerContainer.RelativeChildSize = new Vector2((float)Math.Max(1, beatmap.Value.Track.Length), 1);
                beatmap.ValueChanged += b => markerContainer.RelativeChildSize = new Vector2((float)Math.Max(1, b.Track.Length), 1);
            }

            protected override bool OnDragStart(InputState state) => true;

            protected override bool OnDrag(InputState state)
            {
                seekToPosition(state.Mouse.NativeState.Position);
                return true;
            }

            protected override bool OnDragEnd(InputState state) => true;

            protected override bool OnMouseDown(InputState state, MouseDownEventArgs args)
            {
                seekToPosition(state.Mouse.NativeState.Position);
                return true;
            }

            private void seekToPosition(Vector2 screenPosition)
            {
                float markerPos = MathHelper.Clamp(markerContainer.ToLocalSpace(screenPosition).X, 0, markerContainer.DrawWidth);
                seekTo(markerPos / markerContainer.DrawWidth * beatmap.Value.Track.Length);
            }

            private void seekTo(double time) => beatmap.Value.Track.Seek(time);

            protected override void Update()
            {
                base.Update();

                marker.X = (float)beatmap.Value.Track.CurrentTime;
            }

            private class ControlPointTimeline : Timeline
            {
                protected override void LoadBeatmap(WorkingBeatmap beatmap)
                {
                    ControlPointInfo cpi = beatmap.Beatmap.ControlPointInfo;

                    cpi.TimingPoints.ForEach(addTimingPoint);

                    // Consider all non-timing points as the same type
                    cpi.SoundPoints.Select(c => (ControlPoint)c)
                        .Concat(cpi.EffectPoints)
                        .Concat(cpi.DifficultyPoints)
                        .Distinct()
                        // Non-timing points should not be added where there are timing points
                        .Where(c => cpi.TimingPointAt(c.Time).Time != c.Time)
                        .ForEach(addNonTimingPoint);
                }

                private void addTimingPoint(ControlPoint controlPoint) => Add(new TimingPointVisualisation(controlPoint));
                private void addNonTimingPoint(ControlPoint controlPoint) => Add(new NonTimingPointVisualisation(controlPoint));

                private class TimingPointVisualisation : ControlPointVisualisation
                {
                    public TimingPointVisualisation(ControlPoint controlPoint)
                        : base(controlPoint)
                    {
                    }

                    [BackgroundDependencyLoader]
                    private void load(OsuColour colours) => Colour = colours.YellowDark;
                }

                private class NonTimingPointVisualisation : ControlPointVisualisation
                {
                    public NonTimingPointVisualisation(ControlPoint controlPoint)
                        : base(controlPoint)
                    {
                    }

                    [BackgroundDependencyLoader]
                    private void load(OsuColour colours) => Colour = colours.Green;
                }

                private abstract class ControlPointVisualisation : PointVisualisation
                {
                    public readonly ControlPoint ControlPoint;

                    public ControlPointVisualisation(ControlPoint controlPoint)
                        : base(controlPoint.Time)
                    {
                        ControlPoint = controlPoint;
                    }
                }
            }

            private class BookmarkTimeline : Timeline
            {
                protected override void LoadBeatmap(WorkingBeatmap beatmap)
                {
                    foreach (int bookmark in beatmap.BeatmapInfo.Bookmarks)
                        Add(new BookmarkVisualisation(bookmark));
                }

                private class BookmarkVisualisation : PointVisualisation
                {
                    public BookmarkVisualisation(double startTime)
                        : base(startTime)
                    {
                    }

                    [BackgroundDependencyLoader]
                    private void load(OsuColour colours) => Colour = colours.Blue;
                }
            }

            private class BreakTimeline : Timeline
            {
                protected override void LoadBeatmap(WorkingBeatmap beatmap)
                {
                    foreach (var breakPeriod in beatmap.Beatmap.Breaks)
                        Add(new BreakVisualisation(breakPeriod));
                }

                private class BreakVisualisation : DurationVisualisation
                {
                    public BreakVisualisation(BreakPeriod breakPeriod)
                        : base(breakPeriod.StartTime, breakPeriod.EndTime)
                    {
                    }

                    [BackgroundDependencyLoader]
                    private void load(OsuColour colours) => Colour = colours.Yellow;
                }
            }

            private abstract class Timeline : CompositeDrawable
            {
                private readonly Container timeline;

                public Timeline()
                {
                    AddInternal(timeline = new Container { RelativeSizeAxes = Axes.Both });
                }

                [BackgroundDependencyLoader]
                private void load(OsuGameBase osuGame)
                {
                    osuGame.Beatmap.ValueChanged += b =>
                    {
                        timeline.Clear();
                        timeline.RelativeChildSize = new Vector2((float)Math.Max(1, b.Track.Length), 1);
                        LoadBeatmap(b);
                    };

                    timeline.RelativeChildSize = new Vector2((float)Math.Max(1, osuGame.Beatmap.Value.Track.Length), 1);
                    LoadBeatmap(osuGame.Beatmap);
                }

                protected void Add(PointVisualisation visualisation) => timeline.Add(visualisation);
                protected void Add(DurationVisualisation visualisation) => timeline.Add(visualisation);

                protected abstract void LoadBeatmap(WorkingBeatmap beatmap);
            }

            private class PointVisualisation : Box
            {
                public readonly double StartTime;

                public PointVisualisation(double startTime)
                {
                    StartTime = startTime;

                    Origin = Anchor.TopCentre;

                    RelativeSizeAxes = Axes.Y;
                    Width = 1;
                    EdgeSmoothness = new Vector2(1, 0);

                    RelativePositionAxes = Axes.X;
                    X = (float)startTime;
                }
            }

            private class DurationVisualisation : Container
            {
                public readonly double StartTime;
                public readonly double EndTIme;

                public DurationVisualisation(double startTime, double endTime)
                {
                    StartTime = startTime;
                    EndTIme = endTime;

                    Masking = true;
                    CornerRadius = corner_radius;

                    RelativePositionAxes = Axes.X;
                    RelativeSizeAxes = Axes.Both;
                    X = (float)startTime;
                    Width = (float)(endTime - startTime);

                    AddInternal(new Box { RelativeSizeAxes = Axes.Both });
                }
            }
        }
    }
}

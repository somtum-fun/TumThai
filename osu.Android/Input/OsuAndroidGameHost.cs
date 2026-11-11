// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Android;
using osu.Framework.Input;

namespace osu.Android.Input
{
    internal class OsuAndroidGameHost : AndroidGameHost
    {
        private readonly OsuGameActivity activity;
        private AndroidTextInputSource? textInputSource;

        public OsuAndroidGameHost(OsuGameActivity activity)
            : base(activity)
        {
            this.activity = activity;
        }

        protected override TextInputSource CreateTextInput()
        {
            textInputSource = new AndroidTextInputSource(activity, this);
            return textInputSource;
        }

        protected override void Dispose(bool disposing)
        {
            textInputSource?.Dispose();
            base.Dispose(disposing);
        }
    }
}

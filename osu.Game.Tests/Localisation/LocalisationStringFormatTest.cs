// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Tests.Localisation
{
    [TestFixture]
    public class LocalisationStringFormatTest
    {
        /// <summary>
        /// Establishes that rendering a string is what surfaces a bad format, so the sweep below is
        /// actually capable of failing. A literal brace in a <see cref="TranslatableString"/> has to be
        /// written as <c>{{</c>, because the English text goes through composite formatting.
        /// </summary>
        [Test]
        public void TestUnescapedBraceThrowsWhenRendered()
        {
            LocalisableString broken = new TranslatableString(@"test/unescaped", @"looked up as https://{host}/{userId}");

            Assert.Throws<FormatException>(() => _ = broken.ToString());
        }

        /// <summary>
        /// A bad format string throws only at the moment it is displayed. For a tooltip that means a crash
        /// when the user hovers one particular setting, which nothing else would reach, so every
        /// parameterless string is rendered here.
        /// </summary>
        [Test]
        public void TestEveryParameterlessStringCanBeRendered()
        {
            var failures = new List<string>();
            int rendered = 0;

            foreach (var type in typeof(OnlineSettingsStrings).Assembly.GetTypes())
            {
                if (type.Namespace?.Contains(@".Localisation", StringComparison.Ordinal) != true)
                    continue;

                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (property.PropertyType != typeof(LocalisableString) || property.GetMethod == null)
                        continue;

                    try
                    {
                        _ = property.GetValue(null)!.ToString();
                        rendered++;
                    }
                    catch (Exception e)
                    {
                        failures.Add($@"{type.Name}.{property.Name}: {e.InnerException?.Message ?? e.Message}");
                    }
                }
            }

            Assert.That(rendered, Is.GreaterThan(500), @"expected to have found the localisation strings");
            Assert.That(failures, Is.Empty, () => string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Guards the string that regressed: an unescaped <c>{host}</c> crashed the game whenever the
        /// custom avatar URL setting was hovered.
        /// </summary>
        [Test]
        public void TestCustomAvatarUrlTooltipRendersPlaceholdersLiterally()
        {
            Assert.That(OnlineSettingsStrings.CustomAvatarUrlTooltip.ToString(), Does.Contain(@"https://{host}/{userId}"));
        }
    }
}

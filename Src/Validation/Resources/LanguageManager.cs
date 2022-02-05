#region License

// Copyright (c) .NET Foundation and contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// The latest version of this file can be found at https://github.com/FluentValidation/FluentValidation

#endregion

namespace FastEndpoints.Validation
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;

    /// <summary>
    /// Allows the default error message translations to be managed.
    /// </summary>
    public class LanguageManager : ILanguageManager
    {
        private readonly ConcurrentDictionary<string, string> _languages = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Language factory.
        /// </summary>
        /// <param name="culture">The culture code.</param>
        /// <param name="key">The key to load</param>
        /// <returns>The corresponding Language instance or null.</returns>
        private static string GetTranslation(string culture, string key)
        {
            return culture switch
            {
                EnglishLanguage.AmericanCulture => EnglishLanguage.GetTranslation(key),
                EnglishLanguage.BritishCulture => EnglishLanguage.GetTranslation(key),
                EnglishLanguage.Culture => EnglishLanguage.GetTranslation(key),
                _ => null,
            };
        }

        /// <summary>
        /// Whether localization is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default culture to use for all requests to the LanguageManager. If not specified, uses the current UI culture.
        /// </summary>
        public CultureInfo Culture { get; set; }

        /// <summary>
        /// Removes all languages except the default.
        /// </summary>
        public void Clear()
        {
            _languages.Clear();
        }

        /// <summary>
        /// Gets a translated string based on its key. If the culture is specific and it isn't registered, we try the neutral culture instead.
        /// If no matching culture is found  to be registered we use English.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="culture">The culture to translate into</param>
        /// <returns></returns>
        public virtual string GetString(string key, CultureInfo culture = null)
        {
            string value;

            if (Enabled)
            {
                culture = culture ?? Culture ?? CultureInfo.CurrentUICulture;

                string currentCultureKey = culture.Name + ":" + key;
                value = _languages.GetOrAdd(currentCultureKey, k => GetTranslation(culture.Name, key));

                // If the value couldn't be found, try the parent culture.
                var currentCulture = culture;
                while (value == null && currentCulture.Parent != CultureInfo.InvariantCulture)
                {
                    currentCulture = currentCulture.Parent;
                    string parentCultureKey = currentCulture.Name + ":" + key;
                    value = _languages.GetOrAdd(parentCultureKey, k => GetTranslation(currentCulture.Name, key));
                }

                if (value == null && culture.Name != EnglishLanguage.Culture)
                {
                    // If it couldn't be found, try the fallback English (if we haven't tried it already).
                    if (!culture.IsNeutralCulture && culture.Parent.Name != EnglishLanguage.Culture)
                    {
                        value = _languages.GetOrAdd(EnglishLanguage.Culture + ":" + key, k => EnglishLanguage.GetTranslation(key));
                    }
                }
            }
            else
            {
                value = _languages.GetOrAdd(EnglishLanguage.Culture + ":" + key, k => EnglishLanguage.GetTranslation(key));
            }

            return value ?? string.Empty;
        }

        public void AddTranslation(string language, string key, string message)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException(nameof(language));
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrEmpty(message)) throw new ArgumentNullException(nameof(message));

            _languages[language + ":" + key] = message;
        }
    }
}

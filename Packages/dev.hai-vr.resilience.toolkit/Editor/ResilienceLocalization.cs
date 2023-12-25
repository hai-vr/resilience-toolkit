using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Resilience.Tooklit.Localization.Editor
{
    public class ResilienceLocalization
    {
        private const string EditorLanguageNonLocalized = "Editor Language";
        private const string MetaLanguageNameKey = "_meta_languageName";
        private const string MetaGptKey = "_meta_x_gpt";

        private static readonly string Prefix = LocalePrefix();

        private const string Suffix = ".json";
        private static readonly string LocalePrefsKey = InnerLocalePrefsKey();
        private static readonly string LocaleLocation = Main();
        private static readonly string LocaleLocation2 = Alternate();

        private static string _selectedLanguageCode = "en";
        private static int _selectedIndex;
        
        private static List<string> _availableLanguageNames = new List<string> { "English" };
        private static Dictionary<string, Dictionary<string, string>> _languageCodeToLocalization;
        private static List<string> _availableLanguageCodes;
        
        private static readonly Dictionary<string, string> DebugKeyDatabase = new Dictionary<string, string>();

        private static string LocalePrefix() => "sampletemplate.";
        private static string InnerLocalePrefsKey() => "SampleTemplate.Locale";
        private static string Main() => "Packages/dev.hai-vr.resilience.sampletemplate/ResilienceSDK/SampleTemplate/Scripts/Editor/EditorUI/Locale";
        private static string Alternate() => "Assets/ResilienceSDK/SampleTemplate/Locale";
        private static string IntrospectionAutoFindTypeWithPrefix() => "SampleTemplate";

        static ResilienceLocalization()
        {
            DebugKeyDatabase.Add(MetaLanguageNameKey, "English");
            DebugKeyDatabase.Add(MetaGptKey, @"These can be translated with ChatGPT using the prompt: Please translate the values of this JSON file to language written in the _meta_languageName key. Keep the keys intact. The value of the first key `_meta_languageName` also needs to be translated to that language (for example, French needs to be Français), and then concatenated with the string ` (ChatGPT)` ");
            // DebugKeyDatabase.Add(MetaGptKey, @"These can be translated with ChatGPT using the prompt: Please translate the values of this JSON file to XXXXXXXXX language. Keep the keys intact. The value of the first key `_meta_languageName` needs to be changed to match the XXXXXXXXX language, concatenated with the string ` (ChatGPT)`");
            
            ReloadLocalizationsInternal();
            var confLocale = EditorPrefs.GetString(LocalePrefsKey);
            var languageCode = string.IsNullOrEmpty(confLocale) ? "en" : confLocale;
            if (_languageCodeToLocalization.ContainsKey(languageCode))
            {
                _selectedLanguageCode = languageCode;
            }

            _selectedIndex = _selectedLanguageCode == "en" ? 0 : 1;

            var visited = new HashSet<Type>();
            Introspect(visited);
            PrintDatabase();
        }

        private static void Introspect(HashSet<Type> visited)
        {
            // IntrospectInvokeAllPhrases(typeof(SampleTemplateLocalizationPhrase));
            // IntrospectFields(typeof(SampleTemplateControl), visited);
            // IntrospectFields(typeof(SampleTemplateFolder), visited);
        }

        public static void DisplayLanguageSelector()
        {
            var selectedLanguage = EditorGUILayout.Popup(new GUIContent(EditorLanguageNonLocalized), ActiveLanguageIndex(), AvailableLanguages());
            if (selectedLanguage != ActiveLanguageIndex())
            {
                SwitchLanguage(selectedLanguage);
            }
        }

        public static int ActiveLanguageIndex()
        {
            return _selectedIndex;
        }

        public static string[] AvailableLanguages()
        {
            return _availableLanguageNames.ToArray();
        }

        public static void SwitchLanguage(int selectedLanguage)
        {
            var languageCode = _availableLanguageCodes[selectedLanguage];
            _selectedLanguageCode = languageCode;
            _selectedIndex = selectedLanguage;
            EditorPrefs.SetString(LocalePrefsKey, languageCode);
        }

        private static void ReloadLocalizationsInternal()
        {
            // FIXME: Check if folder exists first
            var localizationGuids = AssetDatabase.FindAssets("", new[] { LocaleLocation, LocaleLocation2 });
            _languageCodeToLocalization = localizationGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    var fileName = Path.GetFileName(path);
                    return fileName.StartsWith(Prefix) && fileName.EndsWith(Suffix);
                })
                .Where(path =>
                {
                    var fileName = Path.GetFileName(path);
                    var languageCode = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
                    return languageCode != "en";
                })
                .ToDictionary(path =>
                {
                    var fileName = Path.GetFileName(path);
                    var languageCode = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
                    return languageCode;
                }, ExtractDictionaryFromPath);

            _availableLanguageCodes = new[] { "en" }
                .Concat(_languageCodeToLocalization.Keys)
                .ToList();
            _availableLanguageNames = new[] { "English" }
                .Concat(_languageCodeToLocalization.Values.Select(dictionary => (dictionary.TryGetValue(MetaLanguageNameKey, out var value) ? value : "??")))
                .ToList();
        }

        private static Dictionary<string, string> ExtractDictionaryFromPath(string path)
        {
            try
            {
                var contents = File.ReadAllText(path);
                return ExtractDictionaryFromText(contents);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new Dictionary<string, string>();
            }
        }

        private static Dictionary<string, string> ExtractDictionaryFromText(string contents)
        {
            var localizations = new Dictionary<string, string>();
            
            // Assume that NewtonsoftJson is available in the project
            var jsonObject = JObject.Parse(contents);
            foreach (var pair in jsonObject)
            {
                var value = pair.Value.Value<string>();
                localizations.Add(pair.Key, value);
            }

            return localizations;
        }
        
        // UI

        private static bool IsEnglish()
        {
            return _selectedIndex == 0;
        }

        public static void LocalizePropertyField(SerializedProperty property, bool ignoreEnum = false, bool specialEnum = false)
        {
            if (!ignoreEnum && property.propertyType == SerializedPropertyType.Enum)
            {
                var newValue = EditorGUILayout.Popup(new GUIContent(LocalizeProperty(property)), property.intValue, property.enumNames.Select(
                    (enumName, i) => LocalizeEnumName(enumName, property.enumDisplayNames[i], specialEnum)).ToArray());
                if (newValue != property.intValue)
                {
                    property.intValue = newValue;
                }
                return;
            }
            
            EditorGUILayout.PropertyField(property, new GUIContent(LocalizeProperty(property)));
        }

        public static void LocalizePropertyFieldRect(Rect rect, SerializedProperty property, bool ignoreEnum = false, bool specialEnum = false)
        {
            if (!ignoreEnum && property.propertyType == SerializedPropertyType.Enum)
            {
                var newValue = EditorGUI.Popup(rect, LocalizeProperty(property), property.intValue, property.enumNames.Select(
                    (enumName, i) => LocalizeEnumName(enumName, property.enumDisplayNames[i], specialEnum)).ToArray());
                if (newValue != property.intValue)
                {
                    property.intValue = newValue;
                }
                return;
            }
            
            EditorGUI.PropertyField(rect, property, new GUIContent(LocalizeProperty(property)));
        }

        public static void LocalizeSlider(SerializedProperty property, float leftValue, float rightValue)
        {
            EditorGUILayout.Slider(property, leftValue, rightValue, new GUIContent(LocalizeProperty(property)));
        }

        public static void LocalizeEnum(Rect position, SerializedProperty property, Type enumType)
        {
            // I don't know how to get the enum type from the serialized property, so it is currently passed as a argument
            
            var newValue = EditorGUI.Popup(position, GUIContent.none, property.intValue, property.enumNames.Select(
                (enumName, i) => LocalizeEnumName($"{enumType.Name}_{enumName}", property.enumDisplayNames[i])).Select(o => new GUIContent(o)).ToArray());
            if (newValue != property.intValue)
            {
                property.intValue = newValue;
            }
        }

        internal static string LocalizeOrElse(string labelName, string orDefault)
        {
            var key = $"label_{labelName}";
            return DoLocalize(orDefault, key);
        }

        private static string LocalizeEnumName(string enumValue, string orDefault, bool specialEnum = false)
        {
            var key = $"enum_{enumValue}";
            var localized = DoLocalize(orDefault, key);
            if (!specialEnum || localized == orDefault)
            {
                return localized;
            }
            else
            {
                return orDefault + $" ({localized})";
            }
        }

        private static string LocalizeProperty(SerializedProperty property)
        {
            var key = $"field_{property.name}";
            var orDefault = property.displayName;
            return DoLocalize(orDefault, key);
        }

        private static string DoLocalize(string orDefault, string key)
        {
            RegisterInKeyDb(orDefault, key);
            if (IsEnglish()) return orDefault;
            if (_languageCodeToLocalization[_selectedLanguageCode].TryGetValue(key, out var value)) return value;
            return orDefault;
        }

        private static void RegisterInKeyDb(string orDefault, string key)
        {
            if (!DebugKeyDatabase.ContainsKey(key))
            {
                DebugKeyDatabase.Add(key, orDefault);

                // PrintDatabase();
            }
        }

        private static void PrintDatabase()
        {
            var sorted = new SortedDictionary<string, string>(DebugKeyDatabase);
            var jsonObject = JObject.FromObject(sorted);
            Debug.Log(jsonObject.ToString());
        }

        private static void IntrospectFields(Type type, HashSet<Type> visited)
        {
            if (visited.Contains(type)) return;
            visited.Add(type);
            
            foreach (var fieldInfo in type.GetFields())
            {
                var fieldName = fieldInfo.Name;
                RegisterInKeyDb(ObjectNames.NicifyVariableName(fieldName), $"field_{fieldName}");

                var subType = fieldInfo.FieldType;
                if (subType.IsArray) subType = subType.GetElementType();
                if (subType.Name.StartsWith(IntrospectionAutoFindTypeWithPrefix()))
                {
                    if (subType.IsEnum)
                    {
                        IntrospectEnum(subType, visited);
                    }
                    else
                    {
                        IntrospectFields(subType, visited);
                    }
                }
            }
        }

        private static void IntrospectEnum(Type type, HashSet<Type> visited)
        {
            if (visited.Contains(type)) return;
            visited.Add(type);
            
            foreach (var enumName in type.GetEnumNames())
            {
                RegisterInKeyDb(ObjectNames.NicifyVariableName(enumName), $"enum_{enumName}");
            }
        }

        private static void IntrospectInvokeAllPhrases(Type type)
        {
            foreach (var methodInfo in type.GetMethods()
                         .Where(info => info.ReturnType == typeof(string))
                         .Where(info => info.IsStatic)
                     )
            {
                methodInfo.Invoke(null, Array.Empty<object>());
            }
        }
    }
}
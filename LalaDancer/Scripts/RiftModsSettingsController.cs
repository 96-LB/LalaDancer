﻿using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using FMODUnity;
using Shared;
using Shared.Audio;
using Shared.MenuOptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace LalaDancer.Scripts;


public class RiftModsSettingsController : MonoBehaviour {
    public static RiftAccessibilitySettingsController Template { get; private set; }
    public static TextButtonOption TextButtonPrefab { get; private set; }
    public static ToggleOption TogglePrefab { get; private set; }
    public static CarouselOptionGroup CarouselPrefab { get; private set; }
    public static CarouselSubOption CarouselOptionPrefab { get; private set; }
    public static SliderOption SliderOptionPrefab { get; private set; }
    public static MenuButtonOption BackButtonPrefab { get; private set; }
    public static bool AllPrefabsLoaded => TextButtonPrefab && TogglePrefab && CarouselPrefab && CarouselOptionPrefab && SliderOptionPrefab && BackButtonPrefab;

    public bool Initialized { get; private set; }
    public GameObject OptionsObj { get; private set; }
    public ScrollableSelectableOptionGroup OptionsGroup { get; private set; }
    public OptionsScreenInputController InputController { get; private set; }
    public MenuButtonOption BackButton { get; private set; }
    public TextButtonOption DescriptionLabel { get; private set; }
    public EventReference CancelSelectionSfx { get; private set; }
    public Dictionary<SelectableOption, string> Descriptions { get; } = [];

    public event Action OnClose;

    public static void LoadPrefabs(
        RiftAccessibilitySettingsController template,
        TextButtonOption accessibilityButton,
        ToggleOption togglePrefab,
        CarouselOptionGroup carouselPrefab,
        CarouselSubOption carouselOptionPrefab,
        SliderOption sliderPrefab,
        MenuButtonOption backButtonPrefab
    ) {
        if(!template) {
            throw new UnityException("Failed to load prefab for mod menu. This is usually loaded by cloning the accessibility settings menu.");
        }
        if(!accessibilityButton) {
            throw new UnityException("Failed to load prefab for text buttons. This is usually loaded by cloning the button for the accessibility settings menu.");
        }
        if(!togglePrefab) {
            throw new UnityException("Failed to load prefab for toggle options. This is usually loaded by cloning a toggle option from the accessibility settings menu.");
        }
        if(!carouselPrefab) {
            throw new UnityException("Failed to load prefab for carousel options. This is usually loaded by cloning a carousel option from the accessibility settings menu.");
        }
        if(!carouselOptionPrefab) {
            throw new UnityException("Failed to load prefab for carousel suboptions. This is usually loaded by cloning a carousel suboption from the accessibility settings menu.");
        }
        if(!sliderPrefab) {
            throw new UnityException("Failed to load prefab for slider options. This is usually loaded by cloning a slider option from the audio settings menu.");
        }
        if(!backButtonPrefab) {
            throw new UnityException("Failed to load prefab for back button. This is usually loaded by cloning the back button from the accessibility settings menu.");
        }

        Template = template;
        TextButtonPrefab = accessibilityButton;
        TogglePrefab = togglePrefab;
        CarouselPrefab = carouselPrefab;
        CarouselOptionPrefab = carouselOptionPrefab;
        SliderOptionPrefab = sliderPrefab;
        BackButtonPrefab = backButtonPrefab;
    }

    public static RiftModsSettingsController Create(string title = "MODS", string name = "ModsSettingsScreen") {
        if(!AllPrefabsLoaded) {
            throw new UnityException($"{nameof(RiftModsSettingsController)} could not be created because not all prefabs are loaded. This usually means that your mod version is outdated. If you are using the latest version, please contact the mod developers.");
        }

        var copy = Instantiate(Template, Template.transform.parent);
        copy.gameObject.SetActive(false);
        copy.gameObject.name = name;

        var controller = copy.gameObject.AddComponent<RiftModsSettingsController>();
        controller.OptionsObj = copy._mainOptionsParent;
        controller.OptionsGroup = copy._scrollableSelectableOptionGroup;
        controller.InputController = copy._optionsScreenInputController;
        controller.CancelSelectionSfx = copy._cancelSelectionSfx;

        Destroy(copy);
        Destroy(controller.transform.Find("ColorBlindnessSubmenu").gameObject);

        // replace the back/confirm button group with just the back button
        var backMenu = (SelectableOptionGroup)controller.InputController._options[1];
        var backButton = (MenuButtonOption)backMenu._options[0];
        var backTransform = backButton.GetComponent<RectTransform>();
        backTransform.SetParent(controller.OptionsObj.transform, false);
        backTransform.pivot = new(1, 0);
        backTransform.anchorMin = backTransform.anchorMax = new(0.5f, 0f);
        backTransform.anchoredPosition = new(-20, 200);
        backTransform.sizeDelta = new(200, 60);
        Destroy(backMenu.gameObject);

        backButton.OnClick += controller.HandleCloseInput;
        backButton.OnClick += controller.PlayCancelSfx;
        controller.BackButton = backButton;

        var descriptionLabel = Instantiate(TextButtonPrefab, controller.OptionsObj.transform);
        var descriptionTransform = descriptionLabel.GetComponent<RectTransform>();
        descriptionLabel.name = $"Label - Mod - {name} - Description";
        descriptionTransform.pivot = new(0, 0);
        descriptionTransform.anchorMin = backTransform.anchorMax = new(0.5f, 0f);
        descriptionTransform.anchoredPosition = new(20, 160);
        descriptionTransform.sizeDelta = new(666, 100);
        foreach(var label in descriptionLabel._textLabels) {
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.fontSize *= 0.55f;
            label.fontSizeMin *= 0.55f;
            label.fontSizeMax *= 0.55f;
            label.enableAutoSizing = false;
            if(label.TryGetComponent<ContentSizeFitter>(out var fitter)) {
                Destroy(fitter);
            }
            var rect = label.GetComponent<RectTransform>();
            rect.pivot = rect.anchorMin = rect.anchorMax = rect.anchoredPosition = new();
            rect.sizeDelta = descriptionTransform.sizeDelta;
        }
        descriptionLabel.SetSelected(false, false);
        controller.DescriptionLabel = descriptionLabel;
        
        // all old options are removed here because of a hidden call to Initialize()
        controller.InputController.TryAddOption(controller.OptionsGroup);
        controller.InputController.TryAddOption(backButton);

        foreach(var opt in controller.OptionsGroup._options) {
            DestroyImmediate(opt.gameObject);
        }
        controller.OptionsGroup.RemoveAllOptions();

        var titleObj = controller.OptionsObj.transform.Find("Menu_Settings_TitleText");
        Util.ForceSetText(titleObj.gameObject, title);

        controller.Initialized = true;
        return controller;
    }

    public void SetRectHeight(SelectableOption opt, float height) {
        if(opt.TryGetComponent<RectTransform>(out var rect)) {
            if(opt.TryGetComponent<ContentSizeFitter>(out var fitter)) {
                Destroy(fitter);
            }
            float delta = height - rect.rect.height;
            OptionsGroup._accumulatedContentSize += delta;
            rect.sizeDelta += Vector2.up * delta;
            Plugin.Log.LogMessage(opt.gameObject.name + " height changed with delta " + delta + " and final rect " + rect.rect);
        }
    }

    public void AddAllModMenus() {
        var plugins = Chainloader.PluginInfos.Values.OrderBy(x => x.Metadata.Name);
        foreach(var plugin in plugins) {
            AddModMenu(plugin);
        }
    }

    public void AddModMenu(PluginInfo plugin) {
        var controller = Create(Util.PascalToSpaced(plugin.Metadata.Name), $"ModSettingsScreen - {plugin.Metadata.Name}");
        controller.AddAllConfigOptions(plugin);

        var button = (TextButtonOption)OptionsGroup.AddOptionFromPrefab(TextButtonPrefab, true);
        button.name = $"TextButton - Mod - {plugin.Metadata.Name}";

        button.OnSubmit += () => {
            OptionsObj.SetActive(false);
            InputController.IsInputDisabled = true;
            controller.gameObject.SetActive(true);
        };

        controller.OnClose += () => {
            if(!enabled) {
                return;
            }
            button.SetSubmitted(false);
            this.ScheduleForNextFrame(() => {
                controller.gameObject.SetActive(false);
                InputController.IsInputDisabled = false;
                OptionsObj.SetActive(true);
            });
        };

        foreach(var label in button._textLabels) {
            Util.ForceSetText(label, Util.PascalToSpaced(plugin.Metadata.Name));
        }
        SetRectHeight(button, 60);

        Descriptions[button] = $"{plugin.Metadata.GUID}\nv{plugin.Metadata.Version}";
    }

    public void AddAllConfigOptions(PluginInfo plugin) {
        var categories = plugin.Instance.Config.Select(x => x.Key.Section).Distinct();
        foreach(var category in categories) {
            AddConfigCategory(plugin, category);
        }
        DebugUtil.PrintAllChildren(OptionsGroup, true, true);
    }

    public void AddConfigCategory(PluginInfo plugin, string category) {
        AddCategoryLabel(plugin, category);
        AddPadding(plugin);
        var options = plugin.Instance.Config.Where(x => x.Key.Section == category);
        foreach(var option in options) {
            AddConfigOption(plugin, option.Key, option.Value);
        }
    }

    public TextButtonOption AddCategoryLabel(PluginInfo plugin, string category) {
        var button = (TextButtonOption)OptionsGroup.AddOptionFromPrefab(TextButtonPrefab, true);
        button.name = $"Label - Mod - {plugin.Metadata.Name} - {category}";

        foreach(var label in button._textLabels) {
            Util.ForceSetText(label, category);
            label.fontStyle |= FontStyles.Italic;
        }

        SetRectHeight(button, 75);

        OptionsGroup.RemoveOption(button);
        Destroy(button); // keeps the GameObject, but not the SelectableOption
        return button;
    }

    public SelectableOption AddConfigOption(PluginInfo plugin, ConfigDefinition key, ConfigEntryBase value) =>
        value switch {
            ConfigEntry<bool> val => AddToggleOption(plugin, key, val),
            ConfigEntry<string> val => AddCarouselOption(plugin, key, val),
            _ when value.SettingType.IsEnum => AddCarouselOption(plugin, key, value, value.SettingType.GetEnumNames()),
            ConfigEntry<int> or ConfigEntry<float> => AddSliderOption(plugin, key, value),
            ConfigEntry<Color> val => AddColorOption(plugin, key, val),
            _ => null
        };

    public ToggleOption AddToggleOption(PluginInfo plugin, ConfigDefinition key, ConfigEntry<bool> value) {
        var button = (ToggleOption)OptionsGroup.AddOptionFromPrefab(TogglePrefab, true);
        button.isOn = value.Value;
        button.name = $"ToggleOption - Mod - {plugin.Metadata.Name} - {key.Section}.{key.Key}";
        button.OnValueChanged += (isOn) => {
            value.Value = isOn;
            Plugin.Log.LogInfo($"Updated config [{key.Section}.{key.Key}] to {isOn}.");
        };
        Util.ForceSetText(button._labelText, key.Key);
        Descriptions[button] = value.Description.Description;
        return button;
    }

    public CarouselOptionGroup AddCarouselOption(PluginInfo plugin, ConfigDefinition key, ConfigEntry<string> value) =>
        value.Description.AcceptableValues switch {
            AcceptableValueList<string> vals => AddCarouselOption(plugin, key, value, vals.AcceptableValues),
            _ => null
        };

    public CarouselOptionGroup AddCarouselOption(PluginInfo plugin, ConfigDefinition key, ConfigEntryBase value, string[] options) {
        var carousel = (CarouselOptionGroup)OptionsGroup.AddOptionFromPrefab(CarouselPrefab, true);
        carousel.name = $"CarouselOption - Mod - {plugin.Metadata.Name} - {key.Section}.{key.Key}";
        carousel.RemoveAllOptions(true);
        var selectedIndex = 0;
        var width = 300f; // minimum width
        foreach(var option in options) {
            if(value.Description.AcceptableValues?.IsValid(option) ?? true) {
                var subOption = Instantiate(CarouselOptionPrefab, carousel.Content);
                subOption.name = $"CarouselSubOption - Mod - {plugin.Metadata.Name} - {key.Section}.{key.Key} - {option}";

                // set text and measure width
                if(subOption._textLabels != null && subOption._textLabels.Length > 0) {
                    var text = Util.PascalToSpaced(option);
                    var label = subOption._textLabels[0];
                    width = Mathf.Max(width, label.GetPreferredValues(text).x + 10f);
                    Util.ForceSetText(label, text);
                }

                carousel.TryAddOption(subOption);
                if(string.Equals(option, value.GetSerializedValue(), StringComparison.InvariantCultureIgnoreCase)) {
                    selectedIndex = carousel.NumberOfOptions - 1;
                }
            }
        }

        // only generate the carousel if there are multiple options to choose from
        if(carousel.NumberOfOptions < 2) {
            carousel.RemoveAllOptions(true);
            Destroy(carousel.gameObject);
            return null;
        }

        // set the title text
        Util.ForceSetText(carousel._title, key.Key);

        // set the width of the buttons
        var rect = carousel.Content.parent.GetComponent<RectTransform>();
        rect.sizeDelta = new(width, rect.sizeDelta.y);
        foreach(var arrow in carousel._arrows) {
            var rect2 = arrow.GetComponent<RectTransform>();
            rect2.anchoredPosition = new((30f + width / 2f) * Mathf.Sign(rect2.anchoredPosition.x), rect2.anchoredPosition.y);
        }

        // reduce the enormous amount of space
        SetRectHeight(carousel, 100);

        // initialize the carousel
        carousel.SetSelectionIndex(selectedIndex);
        carousel.FlagAsExternallyInitialized();
        carousel.SetSelected(false, false);

        // make the carousel set the config value
        carousel.OnSelectedIndexChanged += (index) => {
            value.SetSerializedValue(options[index]);
            Plugin.Log.LogInfo($"Updated config [{key.Section}.{key.Key}] to {index} ({options[index]})");
        };

        Descriptions[carousel] = value.Description.Description;

        return carousel;
    }

    public SliderOption AddSliderOption(PluginInfo plugin, ConfigDefinition key, ConfigEntryBase value) =>
        value.Description.AcceptableValues switch {
            AcceptableValueRange<int> val => AddSliderOption(plugin, key, value, val),
            AcceptableValueRange<float> val => AddSliderOption(plugin, key, value, val),
            _ => null
        };
    
    public SliderOption AddSliderOption(
        PluginInfo plugin,
        ConfigDefinition key,
        ConfigEntryBase value,
        AcceptableValueRange<float> range,
        Action<string, float> onValueChanged = null
    ) {
        var slider = (SliderOption)OptionsGroup.AddOptionFromPrefab(SliderOptionPrefab, true);
        slider.name = $"SliderOption - Mod - {plugin.Metadata.Name} - {key.Section}.{key.Key}";

        slider._displayAsPercentage = false;
        slider._decimals = 3;
        slider._valueMin = range.MinValue;
        slider._valueMax = range.MaxValue;
        slider._valueStep = (range.MaxValue - range.MinValue) / 500f;
        slider._initialUpdateCooldown = 0.03f;
        slider._updateCooldownReductionInterval = 0.01f;
        slider._updateCooldownReductionAmount = 0.00025f;

        slider._value = Convert.ToSingle(value?.BoxedValue);
        Util.ForceSetText(slider._labelText, key.Key);
        slider.HandleValueUpdated();

        slider.OnValueChanged += onValueChanged ?? ((_, num) => {
            num = Mathf.Clamp(Mathf.Round(num * 1e6f) / 1e6f, range.MinValue, range.MaxValue);
            value.SetSerializedValue(num.ToString(CultureInfo.InvariantCulture));
            Plugin.Log.LogInfo($"Updated config [{key.Section}.{key.Key}] to {num}.");
        });

        SetRectHeight(slider, 75);

        Descriptions[slider] = value?.Description.Description;

        return slider;
    }

    public SliderOption AddSliderOption(PluginInfo plugin, ConfigDefinition key, ConfigEntryBase value, AcceptableValueRange<int> range) {
        var slider = AddSliderOption(plugin, key, value, new AcceptableValueRange<float>(range.MinValue, range.MaxValue));
        slider._decimals = 0;
        slider._valueStep = 1;
        slider._initialUpdateCooldown = 0.15f;
        slider._updateCooldownReductionAmount = 0.00125f;
        return slider;
    }

    public TextButtonOption AddColorLabel(PluginInfo plugin, ConfigDefinition key) {
        var button = (TextButtonOption)OptionsGroup.AddOptionFromPrefab(TextButtonPrefab, true);
        button.name = $"Label - Mod - {plugin.Metadata.Name} - {key.Section}.{key.Key}";

        foreach(var label in button._textLabels) {
            Util.ForceSetText(label, key.Key);
            label.fontSize *= 0.75f;
            label.fontSizeMin *= 0.75f;
            label.fontSizeMax *= 0.75f;
        }
        
        SetRectHeight(button, 40);

        OptionsGroup.RemoveOption(button);
        Destroy(button); // keeps the GameObject, but not the SelectableOption
        return button;
    }

    public TextButtonOption AddPadding(PluginInfo plugin, float height = 10) {
        var button = (TextButtonOption)OptionsGroup.AddOptionFromPrefab(TextButtonPrefab, true);
        button.name = $"Padding - Mod - {plugin.Metadata.Name}";

        foreach(var label in button._textLabels) {
            Util.ForceSetText(label, "");
        }

        SetRectHeight(button, height);

        OptionsGroup.RemoveOption(button);
        Destroy(button); // keeps the GameObject, but not the SelectableOption
        return button;
    }

    public TextButtonOption AddColorOption(PluginInfo plugin, ConfigDefinition key, ConfigEntry<Color> value) {
        var header = AddColorLabel(plugin, key);

        var range = new AcceptableValueRange<float>(0f, 1f);
        var sliders = new SliderOption[4];
        var channels = new[] { "Red", "Green", "Blue", "Alpha" };

        void OnValueChanged(string channel, float num) {
            var color = new Color(
                Mathf.Clamp01(sliders[0]._value),
                Mathf.Clamp01(sliders[1]._value),
                Mathf.Clamp01(sliders[2]._value),
                Mathf.Clamp01(sliders[3]._value)
            );

            var opaque = new Color(color.r, color.g, color.b);

            value.Value = color;
            for(int i = 0; i < sliders.Length; i++) {
                var bgColor = i == 3 ? color : opaque;
                sliders[i]._leftTrackSelectedColor = bgColor;
                sliders[i]._rightTrackSelectedColor = bgColor.RGBMultiplied(0.5f);
                if(sliders[i].IsSelected && sliders[i]._leftTrackBackground) {
                    sliders[i]._leftTrackBackground.color = bgColor;
                    sliders[i]._rightTrackBackground.color = bgColor.RGBMultiplied(0.5f);
                }
            }
            if(!string.IsNullOrEmpty(channel)) {
                Plugin.Log.LogInfo($"Updated config [{key.Section}.{key.Key}] {channel} to {num}.");
            }
        }

        for(int i = 0; i < sliders.Length; i++) {
            sliders[i] = AddSliderOption(plugin, key, null, range, OnValueChanged);
            sliders[i].name += " - " + channels[i];
            sliders[i]._value = value.Value[i];
            sliders[i]._valueId = channels[i];
            sliders[i].HandleValueUpdated();
            Util.ForceSetText(sliders[i]._labelText, channels[i]);
            SetRectHeight(sliders[i], 30f);
            Descriptions[sliders[i]] = value.Description.Description;
        }

        OnValueChanged(null, 0);

        var footer = AddPadding(plugin);

        return header; // this is cursed
    }

    public void Awake() {
        if(!Initialized) {
            throw new UnityException($"{nameof(RiftModsSettingsController)} should be created using static {nameof(Create)} method.");
        }

        if(InputController) {
            InputController.OnCloseInput += HandleCloseInput;
        }
    }

    public void Update() {
        if(!Initialized) {
            return;
        }

        var text = "";
        if(OptionsGroup.IsSelected) {
            var index = OptionsGroup._selectionIndex;
            if(0 <= index && index < OptionsGroup._options.Count) {
                Descriptions.TryGetValue(OptionsGroup._options[index], out text);
            }
        }
        Util.ForceSetText(DescriptionLabel._textLabels[0], text);
    }

    public void OnDestroy() {
        if(InputController) {
            InputController.OnCloseInput -= HandleCloseInput;
        }
    }

    public void OnEnable() {
        OptionsObj.SetActive(true);
        InputController.IsInputDisabled = false;
        InputController.SetSelectionIndex(0);
        OptionsGroup.SetSelectionIndex(0);
    }

    public void OnDisable() {
        OptionsObj.SetActive(false);
        InputController.IsInputDisabled = true;
    }

    public void HandleCloseInput() {
        OnClose?.Invoke();
        InputController.SetSelectionIndex(0);
    }

    public void PlayCancelSfx() {
        if(!CancelSelectionSfx.IsNull) {
            AudioManager.Instance.PlayAudioEvent(CancelSelectionSfx, 0f, shouldCache: true, 0u, 0f, shouldApplyLatency: false);
        }
    }
}
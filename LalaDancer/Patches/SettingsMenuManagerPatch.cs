﻿using HarmonyLib;
using LalaDancer.Scripts;
using Shared;
using Shared.MenuOptions;
using Shared.Title;
using System.Collections;
using TicToc.Localization.Components;
using TMPro;
using UnityEngine;

namespace LalaDancer.Patches;

using P = SettingsMenuManager;
using MenuState = State<SettingsMenuManager, MenuStateData>;
using Action = System.Action;

internal class MenuStateData {
    internal TextButtonOption modsButton;
    internal RiftModsSettingsController riftModsSettingsController;
    internal Action HandleOpenModsSettings;
    internal Action HandleModsSettingsClosed;
}

[HarmonyPatch(typeof(P))]
internal static class SettingsMenuManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    internal static void Start(
        P __instance,
        OptionsScreenInputController ____inputController,
        GameObject ____contentParent,
        TextButtonOption ____accessibilityButton,
        RiftAccessibilitySettingsController ____riftAccessibilitySettingsController
    ) {
        // the accessibility button is our template for text buttons
        SettingsMenuManagerPatch_Internal.textButtonPrefab = ____accessibilityButton;
        if(!____accessibilityButton) {
            Plugin.Log.LogError("Failed to find back button on settings menu. Aborting mod settings menu creation.");
            return;
        }

        // not choosing any specific template; just search the hierarchy
        SettingsMenuManagerPatch_Internal.togglePrefab = ____riftAccessibilitySettingsController.GetComponentInChildren<ToggleOption>();
        if(!SettingsMenuManagerPatch_Internal.togglePrefab) {
            Plugin.Log.LogError("Failed to find toggle option in accessibility settings menu. Aborting mod settings menu creation.");
            return;
        }

        // not choosing any specific template; just search the hierarchy
        SettingsMenuManagerPatch_Internal.carouselPrefab = ____riftAccessibilitySettingsController.GetComponentInChildren<CarouselOptionGroup>();
        if(!SettingsMenuManagerPatch_Internal.carouselPrefab) {
            Plugin.Log.LogError("Failed to find carousel option in accessibility settings menu. Aborting mod settings menu creation.");
            return;
        }

        // steal a prefab from the accessibility menu
        SettingsMenuManagerPatch_Internal.carouselOptionPrefab = ____riftAccessibilitySettingsController._backgroundDetailCarouselOptionPrefab;
        if(!SettingsMenuManagerPatch_Internal.carouselOptionPrefab) {
            Plugin.Log.LogError("Failed to load carousel sub-option prefab from accessibility settings menu. Aborting mod settings menu creation.");
            return;
        }

        // the accessibility menu is our template for the new menus
        if(!____riftAccessibilitySettingsController) {
            Plugin.Log.LogError("Failed to find accessibility settings menu. Aborting mod settings menu creation.");
            return;
        }

        var controller = RiftModsSettingsController.Create(____riftAccessibilitySettingsController);
        var HandleOpenModsSettings = SettingsMenuManagerPatch_Internal.HandleOpenModsSettings(____contentParent, controller);
        var HandleModsSettingsClosed = SettingsMenuManagerPatch_Internal.HandleModsSettingsClosed(____contentParent, controller);
        var modsButton = Object.Instantiate(____accessibilityButton, ____accessibilityButton.transform.parent);
        modsButton.name = "TextButton - Mods";
        modsButton.OnSubmit += HandleOpenModsSettings;

        foreach(var label in modsButton._textLabels) {
            // the localizer will try to change the text we set
            // remove it so this doesn't happen
            if(label.TryGetComponent(out BaseLocalizer localizer)) {
                Object.Destroy(localizer);
            }
            label.SetText("MODS");
        }

        Color color = new(196f / 255, 241f / 255, 65f / 255);
        modsButton._selectedTextColor = color;
        modsButton._unselectedTextColor = color * 0.6f;

        // add the button to the input controller and layout group as the penultimate option (before BACK)
        ____inputController.TryAddOption(modsButton, ____inputController.LastOptionIndex);
        var index = modsButton.transform.GetSiblingIndex();
        if(index > 0) {
            modsButton.transform.SetSiblingIndex(index - 1);
        }

        var state = MenuState.Of(__instance);
        state.modsButton = modsButton;
        state.riftModsSettingsController = controller;
        state.HandleOpenModsSettings = HandleOpenModsSettings;
        state.HandleModsSettingsClosed = HandleModsSettingsClosed;

        Plugin.Log.LogInfo("Created mods menu button.");
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnEnable")]
    internal static void OnEnable(P __instance) {
        var state = MenuState.Of(__instance);
        if(state.riftModsSettingsController) {
            state.riftModsSettingsController.OnClose += state.HandleModsSettingsClosed;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnDisable")]
    internal static void OnDisable(P __instance) {
        var state = MenuState.Of(__instance);
        if(state.riftModsSettingsController) {
            state.riftModsSettingsController.OnClose -= state.HandleModsSettingsClosed;
        }
    }
}

internal static class SettingsMenuManagerPatch_Internal {

    internal static TextButtonOption textButtonPrefab;
    internal static ToggleOption togglePrefab;
    internal static CarouselOptionGroup carouselPrefab;
    internal static CarouselSubOption carouselOptionPrefab;

    internal static Action HandleOpenModsSettings(
        GameObject contentParent,
        RiftModsSettingsController riftModsSettingsController
    ) {
        return () => {
            contentParent.SetActive(false);
            riftModsSettingsController.gameObject.SetActive(true);
        };
    }

    internal static Action HandleModsSettingsClosed(
        GameObject contentParent,
        RiftModsSettingsController riftModsSettingsController
    ) {
        return () => {
            riftModsSettingsController.StartCoroutine(CloseModsSettingsRoutine(contentParent, riftModsSettingsController));
        };
    }

    internal static IEnumerator CloseModsSettingsRoutine(
        GameObject contentParent,
        RiftModsSettingsController riftModsSettingsController
    ) {
        yield return null;
        riftModsSettingsController.gameObject.SetActive(false);
        contentParent.SetActive(true);
    }
}

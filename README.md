# Terraria Translations
Unofficial Terraria 1.4.1 Chinese localization.
* `tool`: Tool to extract localizations, and convert between gettext style (`.PO`) and Terraria style (`.json`) localization file.
* `legacy`: Legacy Terraria 1.4 localization files.
* `source`: Terraria 1.4.1 localization files.
* `project`: OmegaT project

### Sample Usage
1. Compile `Chireiden.Terraria.sln`

2. Run `Chireiden.Terraria.Localization asm Terraria.exe false [sourceLanguage] [targetLanguage] -- po <pathPO>`  
   (`de-DE`, `en-US`, `es-ES`, `fr-FR`, `it-IT`, `pl-PL`, `pt-BR`, `ru-RU`, `zh-Hans`)
  
3. Copy the po file created to `project\source`

4. Open OmegaT, start your translation

5. After you done translation, run `Chireiden.Terraria.Localization po <pathPO> -- asm <pathGameExecutable> <targetLanguage>`  
   (Target po file usually locate in `project\target`)

6. Now you should get a patched version of Terraria!  
It will be in the same folder as `<pathGameExecutable>` but called `Terraria_locpatched.exe`

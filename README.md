# Terraria Translations
Unofficial Terraria 1.4 Chinese localization.
* `converter`: Tool to extract localizations, and convert between gettext style (`.PO`) and Terraria style (`.json`) localization file.
* `legacy`: Legacy Terraria 1.3 localization files.
* `source`: Terraria 1.4 localization files.
* `project`: OmegaT project

### Usage
1 Compile `Chireiden.Terraria.sln`
2 Run `Chireiden.Terraria.Localization extract path\to\Terraria.exe sourceLanguage targetLanguage`
  (`de-DE`, `en-US`, `es-ES`, `fr-FR`, `it-IT`, `pl-PL`, `pt-BR`, `ru-RU`, `zh-Hans`)
3 Copy the `output.po` created by converter to `project\source`
4 Open OmegaT, start your translation
5 After you done translation, run `Chireiden.Terraria.Localization repack path\to\Terraria.exe targetLanguage path\to\po`
  (Target po file usually locate in `project\target`)
6 Now you should get a patched version of Terraria! It will be in the same folder as `Terraria.exe` but called `Terraria_locpatched.exe`
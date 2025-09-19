# Visual Studio 2022 Extension: ResX Resource Quick Action

## Überblick
Entwickle eine Visual Studio 2022 Extension, die eine Quick Action (Lightbulb/Strg+.) bereitstellt, um fehlende String-Ressourcen direkt aus dem Code heraus zu ResX-Dateien hinzuzufügen.

## Hauptfunktionalität

### Trigger-Bedingung
Die Extension soll aktiviert werden, wenn:
1. Im C#-Code auf eine Resource-Property zugegriffen wird (z.B. `Strings.MyNewKey` oder `Resources.TestString`)
2. Die Klasse (`Strings` oder `Resources`) als generierte Designer-Klasse einer .resx-Datei erkannt wird
3. Der spezifische Key (`MyNewKey` oder `TestString`) in der Designer-Klasse nicht existiert
4. Dies einen Compiler-Fehler verursacht (typischerweise CS0117)

### Quick Action Verhalten
Wenn die Bedingungen erfüllt sind:
1. Zeige eine Glühbirne (Lightbulb) mit der Option "Fehlende String Resource hinzufügen"
2. Bei Klick öffnet sich ein WPF-Dialog mit:
   - Label: "Resource Key: [KeyName]" (readonly, z.B. "MyNewKey")
   - Eingabefeld: "Deutsche Übersetzung" (oder die entsprechende Sprache der .de.resx)
   - Eingabefeld: "Englische Übersetzung" (oder die Standard-Sprache)
   - Buttons: "OK" und "Abbrechen"

### Sprach-Logik
Die Extension muss intelligent mit verschiedenen ResX-Konfigurationen umgehen:

1. **Standard-Fall**: 
   - `Strings.resx` (Englisch/Standard)
   - `Strings.de.resx` (Deutsch)
   - → Dialog zeigt "Englische Übersetzung" und "Deutsche Übersetzung"

2. **Umgekehrter Fall**:
   - `Strings.resx` (Deutsch/Standard) 
   - `Strings.en.resx` (Englisch)
   - → Dialog zeigt "Deutsche Übersetzung" und "Englische Übersetzung"

3. **Nur eine Datei**:
   - Nur `Strings.resx` vorhanden
   - → Beide Übersetzungen werden in die eine Datei geschrieben
   - → Englisch als Wert, Deutsch als Kommentar (oder umgekehrt je nach Erkennung)

### Resource-Datei Updates
Nach dem Bestätigen im Dialog:
1. Füge den neuen Key mit der entsprechenden Übersetzung in die .resx-Datei(en) ein
2. Trigger automatisch die Regenerierung der Designer.cs Datei
3. Optional: Führe einen inkrementellen Build durch, damit IntelliSense aktualisiert wird

## Technische Implementierung

### Projekt-Struktur
```
ResXQuickActionExtension/
├── source.extension.vsixmanifest
├── ResXQuickActionExtension.csproj
├── Providers/
│   └── ResXCodeActionProvider.cs
├── Actions/
│   └── AddMissingResourceAction.cs
├── Analyzers/
│   └── MissingResourceAnalyzer.cs
├── Dialogs/
│   ├── AddResourceDialog.xaml
│   └── AddResourceDialog.xaml.cs
├── Services/
│   ├── ResXFileService.cs
│   ├── DesignerFileService.cs
│   └── LanguageDetectionService.cs
└── Utilities/
    └── ResXHelper.cs
```

### Kernkomponenten

#### 1. Code Action Provider (`ResXCodeActionProvider.cs`)
```csharp
using Microsoft.VisualStudio.Language.CodeCleanUp;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("ResX Resource Quick Actions")]
[ContentType("CSharp")]
internal class ResXCodeActionProvider : ISuggestedActionsSourceProvider
{
    // Implementierung
}
```

#### 2. Roslyn Analyzer (`MissingResourceAnalyzer.cs`)
- Nutze Roslyn APIs um Zugriffe auf Designer-Klassen zu analysieren
- Erkenne fehlende Keys durch Semantic Model Analysis
- Prüfe ob die Klasse von einer .resx-Datei generiert wurde

#### 3. ResX Service (`ResXFileService.cs`)
Hauptfunktionen:
- Finde zugehörige .resx-Dateien im Projekt
- Lese bestehende Ressourcen
- Füge neue Ressourcen hinzu
- Erkenne Sprachvarianten (de.resx, en.resx, etc.)

#### 4. Dialog (`AddResourceDialog.xaml`)
WPF-Dialog mit:
- Dynamischen Labels basierend auf erkannten Sprachen
- Validierung der Eingaben
- Modern UI mit Visual Studio Theming

### Wichtige NuGet-Pakete
```xml
<PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.0.32112.339" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.8.0" />
<PackageReference Include="Microsoft.VisualStudio.LanguageServices" Version="4.8.0" />
<PackageReference Include="System.Resources.Extensions" Version="8.0.0" />
```

### Implementierungs-Details

#### Erkennung der Designer-Klasse
```csharp
private bool IsResourceDesignerClass(INamedTypeSymbol typeSymbol)
{
    // Prüfe ob die Klasse GeneratedCodeAttribute hat
    // Prüfe ob eine entsprechende .resx Datei existiert
    // Prüfe typische Designer-Klassen Patterns
}
```

#### Sprach-Erkennung
```csharp
private (string defaultLang, string secondaryLang) DetectLanguageConfiguration(string baseName)
{
    // Suche nach .resx Dateien mit dem baseName
    // Analysiere Sprach-Suffixe (.de, .en, .en-US, etc.)
    // Bestimme Standard- und Sekundärsprache
}
```

#### ResX Manipulation
```csharp
private void AddResourceToFile(string resxPath, string key, string value, string comment = null)
{
    using (var resx = new ResXResourceReader(resxPath))
    using (var writer = new ResXResourceWriter(resxPath))
    {
        // Bestehende Ressourcen kopieren
        // Neue Resource hinzufügen
        // Optional: Kommentar hinzufügen
    }
    
    // Trigger Designer.cs Regenerierung
    TriggerDesignerRegeneration(resxPath);
}
```

#### Designer Regenerierung
```csharp
private async Task TriggerDesignerRegeneration(string resxPath)
{
    // Nutze VS SDK um Custom Tool auszuführen
    // Alternativ: Nutze MSBuild APIs
    // Aktualisiere IntelliSense Cache
}
```

## Dialog Design

### XAML Layout
```xml
<Window x:Class="ResXQuickAction.Dialogs.AddResourceDialog"
        Title="Fehlende String Resource hinzufügen"
        Height="250" Width="500">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <TextBlock Grid.Row="0" FontWeight="Bold">
            Resource Key: <Run Text="{Binding ResourceKey}"/>
        </TextBlock>
        
        <Label Grid.Row="1" Content="{Binding FirstLanguageLabel}" Margin="0,10,0,0"/>
        <TextBox Grid.Row="2" Text="{Binding FirstLanguageValue}" />
        
        <Label Grid.Row="3" Content="{Binding SecondLanguageLabel}" Margin="0,10,0,0"/>
        <TextBox Grid.Row="4" Text="{Binding SecondLanguageValue}" VerticalAlignment="Top"/>
        
        <StackPanel Grid.Row="5" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="OK" Width="75" Margin="0,0,10,0" Click="OK_Click"/>
            <Button Content="Abbrechen" Width="75" Click="Cancel_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

## Testing-Szenarien

1. **Standard Setup**: Resources.resx + Resources.de.resx
2. **Umgekehrtes Setup**: Strings.resx (de) + Strings.en.resx
3. **Single File**: Nur eine .resx Datei
4. **Multiple Sprachen**: .resx, .de.resx, .fr.resx, .en.resx
5. **Verschachtelte Namespaces**: MyApp.Resources.Strings
6. **Shared Projects**: Ressourcen in separaten Projekten

## Erweiterte Features (Optional)

1. **Batch-Modus**: Mehrere fehlende Keys gleichzeitig hinzufügen
2. **Translation API Integration**: Automatische Übersetzungsvorschläge
3. **Preview**: Zeige Vorschau der Änderungen vor dem Speichern
4. **Undo Support**: Integration in VS Undo/Redo Stack
5. **Settings**: Konfigurierbare Standard-Sprachen pro Projekt

## Performance-Überlegungen

- Cache Designer-Klassen Informationen
- Lazy Loading der ResX-Dateien
- Asynchrone Operationen für Datei-I/O
- Minimale Roslyn Analyzer Overhead

## Fehlerbehandlung

- Graceful handling wenn .resx Dateien gesperrt sind
- Validierung von Resource Keys (keine Sonderzeichen)
- Backup vor Änderungen
- Klare Fehlermeldungen bei Problemen

## Deployment

1. Erstelle VSIX Package
2. Signiere mit Certificate
3. Teste auf verschiedenen VS 2022 Versionen (17.0+)
4. Veröffentliche im VS Marketplace (optional)

## Lizenz und Dokumentation

- MIT Lizenz empfohlen
- README mit Beispielen und GIFs
- Changelog für Versionen
- Contribution Guidelines
using Gtk;
using Pango;
using GtkSource;

class AppConfig
{
    public bool WordWrap { get; set; }
    public string CustomCssPath { get; set; } = string.Empty;
}

class Program
{
    private static readonly string ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/yanta/config.json");
    private static Label? newNoteLabel;
    private static readonly CssProvider cssProvider = new();
    private static SearchSettings? searchSettings;
    // Used to declare when a note has been modified from its original state
    // instead of saying it is modified when any change is made ever.
    private static string initialNoteContent = string.Empty;

    static AppConfig LoadConfig(Window window)
    {
        if (System.IO.File.Exists(ConfigFilePath))
        {
            string json = System.IO.File.ReadAllText(ConfigFilePath);
            AppConfig config = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();

            // Load custom CSS path
            if (!string.IsNullOrEmpty(config.CustomCssPath))
            {
                try
                {
                    cssProvider.LoadFromPath(config.CustomCssPath);
                }
                catch (GLib.GException ex)
                {
                    Console.WriteLine($"Error loading CSS file: {ex.Message}");

                    // Ask the user if they want to delete the invalid path
                    string error = ex.Message;
                    bool deleteCustomCssPath = ShowDeleteCssDialog(config.CustomCssPath, error, window);

                    if (deleteCustomCssPath)
                    {
                        config.CustomCssPath = string.Empty;
                    }
                }
            }
            else
            {
                // Load the default GTK theme
                using var defaultCssProvider = new Gtk.CssProvider();
                Gtk.StyleContext.AddProviderForScreen(Gdk.Screen.Default, defaultCssProvider, 800);
            }

            return config;
        }
        return new AppConfig();
    }

    static bool ShowDeleteCssDialog(string invalidCssPath, string error, Window window)
    {
        // Escape special characters in error
        string escapedError = GLib.Markup.EscapeText(error);

        using var dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
           $"Error loading CSS file: {invalidCssPath}\n\nDetails:\n{escapedError}\n\nDo you want to delete this custom CSS path from the configuration?");

        var response = (ResponseType)dialog.Run();
        dialog.Destroy();

        return response == ResponseType.Yes;
    }

    static void SaveConfig(AppConfig config)
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        System.IO.File.WriteAllText(ConfigFilePath, json);
    }

    static void Main(string[] args)
    {
        Gtk.Application.Init();
        Gtk.StyleContext.AddProviderForScreen(Gdk.Screen.Default, cssProvider, 800);

        // Create window
        var window = new Window("Yanta");
        window.DefaultSize = new Gdk.Size(800, 600);

        // Load config
        AppConfig config = LoadConfig(window);

        // Keyboard shortcuts
        var accelGroup = new AccelGroup();
        window.AddAccelGroup(accelGroup);

        var notebook = new Notebook();

        var textView = new SourceView();
        textView.StyleContext.AddProvider(cssProvider, Gtk.StyleProviderPriority.Application);

        searchSettings = new SearchSettings();

        searchSettings.WrapAround = true; // Enable wrap around for continuous searching
        searchSettings.AtWordBoundaries = false;
        searchSettings.CaseSensitive = false;

        var scrolledWindow = new ScrolledWindow(null, null);
        scrolledWindow.Add(textView);

        // Load config word wrap setting.
        if (config.WordWrap)
        {
            textView.WrapMode = Gtk.WrapMode.WordChar;
        }
        else
        {
            textView.WrapMode = Gtk.WrapMode.None;
        }

        newNoteLabel = new Label("New note");
        newNoteLabel.StyleContext.AddProvider(cssProvider, Gtk.StyleProviderPriority.Application);
        notebook.AppendPage(scrolledWindow, newNoteLabel);

        var menuBar = new MenuBar();

        var fileMenu = new Menu();
        var fileMenuItem = new MenuItem("File");
        fileMenuItem.Submenu = fileMenu;

        // Used to overwrite a file if it has been opened through the "Open" option,
        // instead of prompting the user WHERE to save the file.
        bool isFileOpened = false;
        string openedFilePath = string.Empty;

        // Used so we don't prompt the user WHERE to save the file if it has already been saved once.
        bool isFileSaved = false;

        // Used to check if the contents of a file has been modified, but not saved yet
        bool isFileModified = false;

        // Check if the application is launched with file arguments.
        // For example, allows opening a file in yanta from a file manager.
        if (args.Length > 0)
        {
            string filePath = args[0];
            if (System.IO.File.Exists(filePath) && Path.GetExtension(filePath) == ".txt")
            {
                try
                {
                    string fileContent = System.IO.File.ReadAllText(filePath);
                    textView.Buffer.Text = fileContent;
                    initialNoteContent = fileContent;
                    newNoteLabel.Text = Path.GetFileName(filePath);

                    isFileOpened = true;
                    isFileSaved = false;
                    openedFilePath = filePath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening file: {ex.Message}");
                }
            }
        }

        // Add word, paragraph, line & character count to the window
        // Alignment for positioning
        var alignment = new Gtk.Alignment(1, 1, 0, 0)
        {
            RightPadding = 10,
            BottomPadding = 10
        };

        // Labels
        var lineCountLabel = new Label();
        var paragraphCountLabel = new Label();
        var wordCountLabel = new Label();
        var charCountLabel = new Label();

        // Box for all counters
        var countBox = new HBox();
        countBox.PackStart(lineCountLabel, false, false, 5);
        countBox.PackStart(paragraphCountLabel, false, false, 5);
        countBox.PackStart(wordCountLabel, false, false, 5);
        countBox.PackStart(charCountLabel, false, false, 5);
        alignment.Add(countBox); // add the alignment to the countBox

        textView.Buffer.Changed += (sender, e) =>
        {
            string currentContent = textView.Buffer.Text;
            bool contentChanged = currentContent != initialNoteContent;
            isFileModified = contentChanged;

            UpdateWindowTitle(window, newNoteLabel, isFileModified);

            // Update line, paragraph & word count
            string text = textView.Buffer.Text;

            long lineCount = textView.Buffer.LineCount;
            lineCountLabel.Text = $"Lines: {lineCount}";

            long paragraphCount = CountParagraphs(text);
            paragraphCountLabel.Text = $"Paragraphs: {paragraphCount}";

            long wordCount = CountWords(text);
            wordCountLabel.Text = $"Words: {wordCount}";

            long charCount = text.Length;
            charCountLabel.Text = $"Characters: {charCount}";
        };

        static int CountWords(string text)
        {
            string[] words = text.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length;
        }

        // Function to count paragraphs
        static int CountParagraphs(string text)
        {
            string[] paragraphs = text.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            return paragraphs.Length;
        }

        // Allow opening file
        var openMenuItem = new MenuItem("Open");
        openMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.o, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        openMenuItem.Activated += (sender, e) =>
        {
            using var fileChooser = new FileChooserDialog(
                "Open File",
                window,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept);

            fileChooser.Filter = new FileFilter();
            fileChooser.Filter.AddPattern("*.txt");
            fileChooser.Filter.Name = "Text files (*.txt)";

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                string filePath = fileChooser.Filename;
                string fileContent = System.IO.File.ReadAllText(filePath);
                textView.Buffer.Text = fileContent;
                initialNoteContent = fileContent;
                newNoteLabel.Text = Path.GetFileName(filePath);

                isFileOpened = true;
                isFileSaved = false;
                openedFilePath = filePath;
            }
        };
        fileMenu.Append(openMenuItem);

        // Allow saving file
        var saveMenuItem = new MenuItem("Save");
        saveMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.s, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        saveMenuItem.Activated += (sender, e) =>
        {
            var textBuffer = textView.Buffer;
            string noteText = textBuffer.Text;

            if (isFileOpened)
            {
                System.IO.File.WriteAllText(openedFilePath, noteText);
                isFileSaved = true;
                isFileModified = false;
                UpdateWindowTitle(window, newNoteLabel, isFileModified);
            }
            else if (isFileSaved)
            {
                System.IO.File.WriteAllText(newNoteLabel.Text, noteText);
                isFileModified = false;
                UpdateWindowTitle(window, newNoteLabel, isFileModified);
            }
            else
            {
                using var fileChooser = new FileChooserDialog(
                    "Save File",
                    window,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept);

                fileChooser.CurrentName = "untitled.txt";
                fileChooser.Filter = new FileFilter();
                fileChooser.Filter.AddPattern("*.txt");
                fileChooser.Filter.Name = "Text files (*.txt)";

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    string filePath = fileChooser.Filename;
                    System.IO.File.WriteAllText(filePath, noteText);
                    newNoteLabel.Text = Path.GetFileName(filePath);
                    isFileSaved = true;
                }
            }
        };
        fileMenu.Append(saveMenuItem);
        menuBar.Append(fileMenuItem);

        // Add "Save as..." option.
        var saveAsMenuItem = new MenuItem("Save As...");
        saveAsMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.S, Gdk.ModifierType.ControlMask | Gdk.ModifierType.ShiftMask, AccelFlags.Visible);
        saveAsMenuItem.Activated += (sender, e) =>
        {
            var textBuffer = textView.Buffer;
            string noteText = textBuffer.Text;

            using var fileChooser = new FileChooserDialog(
                "Save File As...",
                window,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Save", ResponseType.Accept);

            fileChooser.CurrentName = newNoteLabel.Text + ".txt"; // Default to the current file name
            fileChooser.Filter = new FileFilter();
            fileChooser.Filter.AddPattern("*.txt");
            fileChooser.Filter.Name = "Text files (*.txt)";

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                string filePath = fileChooser.Filename;
                System.IO.File.WriteAllText(filePath, noteText);
                newNoteLabel.Text = Path.GetFileName(filePath);
                isFileSaved = true;
            }
        };
        fileMenu.Append(saveAsMenuItem);

        // Add "Quit" option
        var quitMenuItem = new MenuItem("Quit");
        quitMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.Q, Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        quitMenuItem.Activated += (sender, e) =>
        {
            if (isFileModified)
            {
                using var dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
                $"This file has been modifed, do you wish to save it before closing?");

                dialog.Realized += (sender, e) =>
                {
                    // Get the main window's position
                    window.GetPosition(out int mainWindowX, out int mainWindowY);

                    // Calculate the center position for the dialog within the main window
                    int dialogX = mainWindowX + (window.Allocation.Width - dialog.Allocation.Width) / 2;
                    int dialogY = mainWindowY + (window.Allocation.Height - dialog.Allocation.Height) / 2;

                    dialog.Move(dialogX, dialogY);
                };

                var response = (ResponseType)dialog.Run();
                dialog.Destroy();

                if (response == ResponseType.Yes)
                {
                    SaveFile(textView, newNoteLabel, openedFilePath);
                }
            }

            SaveConfig(config);
            Gtk.Application.Quit();
        };
        fileMenu.Append(new SeparatorMenuItem());
        fileMenu.Append(quitMenuItem);

        var editMenu = new Menu();
        var editMenuItem = new MenuItem("Edit");
        editMenuItem.Submenu = editMenu;

        var undoMenuItem = new MenuItem("Undo");
        var redoMenuItem = new MenuItem("Redo");

        undoMenuItem.Activated += (sender, e) =>
        {
            textView.Buffer.Undo();
        };

        redoMenuItem.Activated += (sender, e) =>
        {
            textView.Buffer.Redo();
        };

        undoMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.z, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        redoMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.Y, Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        editMenu.Append(undoMenuItem);
        editMenu.Append(redoMenuItem);
        editMenu.Append(new SeparatorMenuItem());

        var cutMenuItem = new MenuItem("Cut");
        var copyMenuItem = new MenuItem("Copy");
        var pasteMenuItem = new MenuItem("Paste");

        cutMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.x, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        copyMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.c, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        pasteMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.v, Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        cutMenuItem.Activated += (sender, e) =>
        {
            var clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", false));
            textView.Buffer.CutClipboard(clipboard, true);
        };

        copyMenuItem.Activated += (sender, e) =>
        {
            var clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", false));
            textView.Buffer.CopyClipboard(clipboard);
        };

        pasteMenuItem.Activated += (sender, e) =>
        {
            var clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", false));
            var buffer = textView.Buffer;
            var iter = buffer.GetIterAtMark(buffer.InsertMark);
            buffer.PasteClipboard(clipboard, ref iter, true);
        };

        editMenu.Append(cutMenuItem);
        editMenu.Append(copyMenuItem);
        editMenu.Append(pasteMenuItem);
        editMenu.Append(new SeparatorMenuItem());

        var findMenuItem = new MenuItem("Find");
        findMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.f, Gdk.ModifierType.ControlMask, AccelFlags.Visible);
        findMenuItem.Activated += (sender, e) => ShowFindDialog(window, textView);
        editMenu.Append(findMenuItem);
        editMenu.Append(new SeparatorMenuItem());

        // Add "Preferences" submenu
        var preferencesMenu = new Menu();
        var preferencesMenuItem = new MenuItem("Preferences");
        preferencesMenuItem.Submenu = preferencesMenu;
        editMenu.Append(preferencesMenuItem);

        // Add "Upload theme CSS" option under "Preferences"
        var uploadCssMenuItem = new MenuItem("Upload theme CSS");
        uploadCssMenuItem.Activated += (sender, e) =>
        {
            using var fileChooser = new FileChooserDialog(
                "Upload Theme CSS",
                window,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Upload", ResponseType.Accept);

            fileChooser.Filter = new FileFilter();
            fileChooser.Filter.AddPattern("*.css");
            fileChooser.Filter.Name = "CSS files (*.css)";

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                string cssPath = fileChooser.Filename;
                try
                {
                    // Apply the new theme to the UI
                    ApplyThemeToUI(textView, newNoteLabel);
                    config.CustomCssPath = cssPath;
                    cssProvider.LoadFromPath(cssPath);
                }
                catch (GLib.GException ex)
                {
                    Console.WriteLine($"Error loading CSS file: {ex.Message}");
                    ShowErrorMessageDialog("Invalid CSS file", "The selected CSS file contains errors and could not be loaded. Please make sure the CSS file you are uploading is from a gtk-3.x theme, and does not point to another file.\n\nFalling back to default system theme.", window);
                    config.CustomCssPath = string.Empty;
                }
            }
        };
        preferencesMenu.Append(uploadCssMenuItem);
        menuBar.Append(editMenuItem);

        static void ApplyThemeToUI(SourceView textView, Label newNoteLabel)
        {
            textView.QueueDraw();
            newNoteLabel.QueueDraw();
        }

        static void ShowErrorMessageDialog(string title, string message, Window window)
        {
            using var errorDialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, message);
            errorDialog.Title = title;

            errorDialog.Realized += (sender, e) =>
            {
                window.GetPosition(out int mainWindowX, out int mainWindowY);

                int dialogX = mainWindowX + (window.Allocation.Width - errorDialog.Allocation.Width) / 2;
                int dialogY = mainWindowY + (window.Allocation.Height - errorDialog.Allocation.Height) / 2;

                errorDialog.Move(dialogX, dialogY);
            };

            errorDialog.Run();
            errorDialog.Destroy();
        }

        var viewMenu = new Menu();
        var viewMenuItem = new MenuItem("View");
        viewMenuItem.Submenu = viewMenu;

        // Add Zoom In and Zoom Out options
        var zoomInMenuItem = new MenuItem("Zoom In");
        zoomInMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.equal, Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        var zoomOutMenuItem = new MenuItem("Zoom Out");
        zoomOutMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.minus, Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        var resetZoomMenuItem = new MenuItem("Reset Zoom");
        resetZoomMenuItem.AddAccelerator("activate", accelGroup, '0', Gdk.ModifierType.ControlMask, AccelFlags.Visible);

        viewMenu.Append(zoomInMenuItem);
        viewMenu.Append(zoomOutMenuItem);
        viewMenu.Append(resetZoomMenuItem);
        viewMenu.Append(new SeparatorMenuItem());

        // Default zoom level
        double zoomLevel = 10.0;
        double defaultZoom = 10.0;

        // Create a TextTag for zooming
        var zoomTag = new TextTag("zoom");
        textView.Buffer.TagTable.Add(zoomTag);

        zoomInMenuItem.Activated += (sender, e) =>
        {
            zoomLevel += 5;
            UpdateZoom(textView, zoomTag, zoomLevel, config);
        };

        zoomOutMenuItem.Activated += (sender, e) =>
        {
            if (zoomLevel > 0.1)
            {
                zoomLevel -= 5;
                UpdateZoom(textView, zoomTag, zoomLevel, config);
            }
        };

        resetZoomMenuItem.Activated += (sender, e) =>
        {
            zoomLevel = defaultZoom;
            UpdateZoom(textView, zoomTag, defaultZoom, config);
        };

        // Add word wrap toggle
        var wordWrapMenuItem = new CheckMenuItem("Word Wrap");
        wordWrapMenuItem.AddAccelerator("activate", accelGroup, (uint)Gdk.Key.Z, Gdk.ModifierType.Mod1Mask, AccelFlags.Visible);
        wordWrapMenuItem.Active = config.WordWrap;
        wordWrapMenuItem.Toggled += (sender, e) =>
        {
            config.WordWrap = wordWrapMenuItem.Active;

            // Enable or disable word wrap
            if (config.WordWrap)
            {
                textView.WrapMode = Gtk.WrapMode.WordChar;
            }
            else
            {
                textView.WrapMode = Gtk.WrapMode.None;
            }
        };

        viewMenu.Append(wordWrapMenuItem);
        menuBar.Append(viewMenuItem);

        window.DeleteEvent += (o, args) =>
        {

            if (isFileModified)
            {
                using var dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
                $"This file has been modifed, do you wish to save it before closing?");

                dialog.Realized += (sender, e) =>
                {
                    // Get the main window's position
                    window.GetPosition(out int mainWindowX, out int mainWindowY);

                    int dialogX = mainWindowX + (window.Allocation.Width - dialog.Allocation.Width) / 2;
                    int dialogY = mainWindowY + (window.Allocation.Height - dialog.Allocation.Height) / 2;

                    dialog.Move(dialogX, dialogY);
                };

                var response = (ResponseType)dialog.Run();
                dialog.Destroy();

                if (response == ResponseType.Yes)
                {
                    SaveFile(textView, newNoteLabel, openedFilePath);
                }
            }

            SaveConfig(config);
            Gtk.Application.Quit();
        };

        void SaveFile(SourceView textView, Label newNoteLabel, string filePath)
        {
            var textBuffer = textView.Buffer;
            string noteText = textBuffer.Text;

            if (!string.IsNullOrEmpty(filePath))
            {
                // Save changes to the existing file
                System.IO.File.WriteAllText(filePath, noteText);
                isFileSaved = true;
                isFileModified = false;
                UpdateWindowTitle(window, newNoteLabel, isFileModified);
            }
            else
            {
                // Prompt the user to choose a file path for saving
                using var fileChooser = new FileChooserDialog(
                    "Save File",
                    window,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept);

                fileChooser.CurrentName = newNoteLabel.Text + ".txt"; // Default to the current file name
                fileChooser.Filter = new FileFilter();
                fileChooser.Filter.AddPattern("*.txt");
                fileChooser.Filter.Name = "Text files (*.txt)";

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    string newFilePath = fileChooser.Filename;
                    System.IO.File.WriteAllText(newFilePath, noteText);
                    newNoteLabel.Text = Path.GetFileName(newFilePath);
                    isFileSaved = true;
                    isFileModified = false;
                    UpdateWindowTitle(window, newNoteLabel, isFileModified);
                }
            }
        }

        // Box for everything
        var mainBox = new VBox();
        mainBox.PackStart(menuBar, false, false, 0);
        mainBox.PackStart(notebook, true, true, 0);
        mainBox.PackEnd(countBox, false, false, 0);
        mainBox.PackEnd(countBox, false, false, 0);
        mainBox.PackEnd(alignment, false, false, 0); // alignment for character, line, etc. counters.
        window.Add(mainBox);

        window.ShowAll();

        Gtk.Application.Run();
    }

    // Update zoom level
    static void UpdateZoom(SourceView textView, TextTag tag, double zoomLevel, AppConfig config)
    {
        double maxZoom = 200.0;
        double minZoom = 5.0;

        // Cap zoom level to maxZoom (200.0)
        zoomLevel = Math.Min(zoomLevel, maxZoom);

        // Ensure zoom level doesn't go below minZoom (5.0)
        zoomLevel = Math.Max(zoomLevel, minZoom);

        int fontSize = (int)(zoomLevel * Pango.Scale.PangoScale);
        FontDescription fontDesc = new()
        {
            Size = fontSize
        };

        // Apply the new font description
        textView.ModifyFont(fontDesc);

        if (config != null && !string.IsNullOrEmpty(config.CustomCssPath) && System.IO.File.Exists(config.CustomCssPath))
        {
            try
            {
                cssProvider.LoadFromPath(config.CustomCssPath);
            }
            catch (GLib.GException ex)
            {
                Console.WriteLine($"Error loading CSS file: {ex.Message}");
            }
        }

        // Force update
        textView.QueueDraw();
    }

    static void UpdateWindowTitle(Window mainWindow, Label newNoteLabel, bool isModified)
    {
        string labelText = newNoteLabel.Text;

        if (isModified && !labelText.EndsWith("*"))
        {
            labelText += " *";
        }
        else if (!isModified && labelText.EndsWith("*"))
        {
            labelText = labelText[..^2];
        }

        newNoteLabel.Text = labelText;
        mainWindow.Title = "Yanta: " + labelText;
    }

    private static List<TextIter> searchMatches = new List<TextIter>();
    private static int currentMatchIndex = -1;

    static void FindText(SourceView textView, string searchText, bool matchCase, bool matchWholeWord)
    {
        var parentWindow = textView.Toplevel as Window;

        var buffer = textView.Buffer;
        var startIter = buffer.GetIterAtOffset(0);

        searchMatches.Clear();
        currentMatchIndex = -1;

        var text = buffer.Text;

        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int matchIndex = 0;
        while ((matchIndex = text.IndexOf(searchText, matchIndex, comparison)) != -1)
        {
            bool isWholeWord = true;

            // Check for whole word match
            if (matchWholeWord)
            {
                if (matchIndex > 0 && char.IsLetterOrDigit(text[matchIndex - 1]))
                {
                    isWholeWord = false;
                }
                else if (matchIndex + searchText.Length < text.Length &&
                         char.IsLetterOrDigit(text[matchIndex + searchText.Length]))
                {
                    isWholeWord = false;
                }
            }

            if (isWholeWord)
            {
                var matchStart = buffer.GetIterAtOffset(matchIndex);
                searchMatches.Add(matchStart);
            }

            matchIndex += searchText.Length;
        }

        if (searchMatches.Count > 0)
        {
            currentMatchIndex = 0;
            buffer.SelectRange(searchMatches[currentMatchIndex], buffer.GetIterAtOffset(searchMatches[currentMatchIndex].Offset + searchText.Length));
            textView.ScrollToIter(searchMatches[currentMatchIndex], 0, false, 0, 0);
        }
        else
        {
            ShowInfoMessageDialog(parentWindow, "Not Found", $"Text '{searchText}' not found.");
        }
    }

    static void FindNext(SourceView textView, string searchText, bool matchCase, bool matchWholeWord)
    {
        var buffer = textView.Buffer;
        if (searchMatches.Count > 0)
        {
            currentMatchIndex = (currentMatchIndex + 1) % searchMatches.Count;

            while (!IsMatchAtIndex(buffer, searchText, matchCase, matchWholeWord, currentMatchIndex))
            {
                currentMatchIndex = (currentMatchIndex + 1) % searchMatches.Count;
            }

            // Select and scroll to the filtered match
            buffer.SelectRange(searchMatches[currentMatchIndex], buffer.GetIterAtOffset(searchMatches[currentMatchIndex].Offset + searchText.Length));
            textView.ScrollToIter(searchMatches[currentMatchIndex], 0, false, 0, 0);
        }
    }

    static void FindPrevious(SourceView textView, string searchText, bool matchCase, bool matchWholeWord)
    {
        var buffer = textView.Buffer;
        if (searchMatches.Count > 0)
        {
            currentMatchIndex = (currentMatchIndex - 1 + searchMatches.Count) % searchMatches.Count;

            while (!IsMatchAtIndex(buffer, searchText, matchCase, matchWholeWord, currentMatchIndex))
            {
                currentMatchIndex = (currentMatchIndex - 1 + searchMatches.Count) % searchMatches.Count;
            }

            // Select and scroll to the filtered match
            buffer.SelectRange(searchMatches[currentMatchIndex], buffer.GetIterAtOffset(searchMatches[currentMatchIndex].Offset + searchText.Length));
            textView.ScrollToIter(searchMatches[currentMatchIndex], 0, false, 0, 0);
        }
    }

    static bool IsMatchAtIndex(TextBuffer buffer, string searchText, bool matchCase, bool matchWholeWord, int index)
    {
        var text = buffer.Text;

        // Get the match start position
        var matchStart = buffer.GetIterAtOffset(searchMatches[index].Offset);

        if (matchWholeWord)
        {
            if ((matchStart.Offset > 0 && char.IsLetterOrDigit(text[matchStart.Offset - 1])) ||
                (matchStart.Offset + searchText.Length < text.Length &&
                char.IsLetterOrDigit(text[matchStart.Offset + searchText.Length])))
            {
                return false;
            }
        }

        // Check for case sensitivity and find the text match
        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.IndexOf(searchText, matchStart.Offset, comparison) == matchStart.Offset;
    }

    static void HandleFindDialogResponse(Dialog dialog, SourceView textView, Entry entry, bool matchCase, bool matchWholeWord, ResponseType responseId)
    {
        if (responseId == ResponseType.Ok)
        {
            FindText(textView, entry.Text, matchCase, matchWholeWord);
        }
        else if (responseId == ResponseType.Ok + 1)
        {
            FindNext(textView, entry.Text, matchCase, matchWholeWord);
        }
        else if (responseId == ResponseType.Ok + 2)
        {
            FindPrevious(textView, entry.Text, matchCase, matchWholeWord);
        }
        else if (responseId == ResponseType.Cancel)
        {
            dialog.Hide();
        }
    }

    static void ShowFindDialog(Window parentWindow, SourceView textView)
    {
        using var dialog = new Dialog("Find", parentWindow, DialogFlags.Modal);

        var entry = new Entry();

        var hbox = new Box(Orientation.Horizontal, 0);
        dialog.ContentArea.PackStart(entry, true, true, 5);
        dialog.ContentArea.PackStart(hbox, false, false, 3);

        var matchCaseCheck = new CheckButton("Match Case");
        var matchWholeWordCheck = new CheckButton("Match Whole Word");

        var findButton = new Button("Find");
        var nextButton = new Button("Next");
        var prevButton = new Button("Previous");
        var cancelButton = new Button("Cancel");

        dialog.ContentArea.PackStart(matchCaseCheck, false, false, 5);
        dialog.ContentArea.PackStart(matchWholeWordCheck, false, false, 5);

        hbox.PackStart(findButton, true, true, 3);
        hbox.PackStart(nextButton, true, true, 3);
        hbox.PackStart(prevButton, true, true, 3);
        hbox.PackStart(cancelButton, true, true, 3);

        findButton.Clicked += (s, e) => HandleFindDialogResponse(dialog, textView, entry, matchCaseCheck.Active, matchWholeWordCheck.Active, ResponseType.Ok);
        nextButton.Clicked += (s, e) => HandleFindDialogResponse(dialog, textView, entry, matchCaseCheck.Active, matchWholeWordCheck.Active, ResponseType.Ok + 1);
        prevButton.Clicked += (s, e) => HandleFindDialogResponse(dialog, textView, entry, matchCaseCheck.Active, matchWholeWordCheck.Active, ResponseType.Ok + 2);
        cancelButton.Clicked += (s, e) => HandleFindDialogResponse(dialog, textView, entry, matchCaseCheck.Active, matchWholeWordCheck.Active, ResponseType.Cancel);

        dialog.ShowAll();
        dialog.Run();
    }

    static void ShowInfoMessageDialog(Window parentWindow, string title, string message)
    {
        using var infoDialog = new MessageDialog(parentWindow, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, message);
        infoDialog.Title = title;
        infoDialog.Run();
        infoDialog.Destroy();
    }
}

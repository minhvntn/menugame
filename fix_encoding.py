import os
import re

files = [
    'src/GameLauncher.Client/Forms/MainForm.Layout.cs',
    'src/GameUpdater.WinForms/Forms/MainForm.Layout.cs',
    'src/GameUpdater.WinForms/Forms/GameEditorForm.cs',
    'src/GameUpdater.WinForms/Forms/TargetDriveSelectionForm.cs'
]

for file in files:
    with open(file, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Prepend using if needed
    if 'GameLauncher.Client' in file and 'using GameLauncher.Client.Extensions;' not in content:
        content = 'using GameLauncher.Client.Extensions;\n' + content
    if 'GameUpdater.WinForms' in file and 'using GameUpdater.WinForms.Extensions;' not in content:
        content = 'using GameUpdater.WinForms.Extensions;\n' + content

    content = re.sub(r'new Size\(', 'this.ScaleSize(', content)
    content = re.sub(r'new Point\(', 'this.ScalePoint(', content)
    content = re.sub(r'new Padding\(', 'this.ScalePadding(', content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)

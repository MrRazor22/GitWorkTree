# Git Worktree Manager

A Visual Studio extension that provides an intuitive interface for managing Git worktrees directly within the IDE.

📦 Install
===
| IDE | Marketplace | Description |
| :--- | :--- | :--- |
| **Visual Studio** | [Git Worktree Manager](https://marketplace.visualstudio.com/items?itemName=MrRazer22.git-worktree) | Extension for Visual Studio 2022 (17.0+) & Visual Studio 2026 |

---

## 🌟 Why Git Worktree Manager?

Visual Studio's default Git interface doesn't natively expose worktree management. This extension bridges that gap:

* **Zero Setup & Dependencies**: You **don't need Git installed on your PC** or configured in your `PATH`—it uses Visual Studio's built-in Git automatically.
* **Side-by-Side Multi-Branch Workflows**: Work on multiple features or bug fixes simultaneously in separate, clean folders without stash/checkout/rebuild cycles.
* **Seamless VS Integration**: Create, switch, prune, and manage worktrees entirely from the IDE. Open solutions in new Visual Studio instances with a single click.
* **Repository-Aware Hierarchy**: Easily browse all your worktrees cleanly grouped by parent repository and branch.
* **Smart Path Display**: Automatically shows the shortest unique path for each worktree, making them easy to identify at a glance even when sharing common parent directories.

---

## 📍 Accessing Git Worktrees

Access the Git Worktree tools directly from the Visual Studio menu under **Git > Git Worktree** or use the default hotkeys:

* **Create Worktree**: `Ctrl+Shift+=`
* **Manage Worktrees**: `Ctrl+Shift+\`

![Menu Access](https://github.com/user-attachments/assets/838028e6-179b-4f01-89fd-7c8ce4d18fdc)

---

## ➕ Create Worktrees

Create new worktrees easily with the **Create Worktree** dialog:

* **Dual Creation Modes**: Easily toggle between checking out a **Base Branch** (to review/debug an existing branch directly) or creating a **New Branch** (to start new work, letting you select any branch or Git tag as the base reference).
* Choose any branch or Git tag from the searchable list.
* Path is auto-generated based on the branch name, or you can customize it.
* Choose how to open the new worktree (Open in Current Window, Open in New VS Window, or Open in File Explorer).

![Create Worktree](https://github.com/user-attachments/assets/2066f619-9922-4bbe-b6d5-7c3d4b99fbd2)

---

## 🌳 Manage Worktrees

Manage all your worktrees in a dedicated sidebar/tool window:

* Displays worktrees grouped by repository.
* Clearly identifies the current and main worktrees with badges (CURRENT, MAIN).
* Integrated search and filter bar lets you locate worktrees quickly by branch or folder name.

![Manage Worktrees](https://github.com/user-attachments/assets/f8693857-ce44-4fd6-9969-1fb79bcf4b2a)

---

## 📊 Worktree Details

Selecting a worktree reveals its details, helping you see its status without opening the worktree:

* **Staged & Changes**: View files that have changes.
* **Untracked Files**: View new untracked files.
* **Outgoing Commits**: Inspect commits that are ready to push.

![Worktree Details](https://github.com/user-attachments/assets/96ac6cd5-1565-4683-a05c-fe06504b439f)

---

## 🚀 Quick Actions

Right-click any worktree node to trigger context menu actions:

* **Open in Visual Studio**: Open the selected worktree in a new Visual Studio instance.
* **Open in Explorer**: Reveal the folder in Windows File Explorer.
* **Copy Path**: Copy the absolute worktree path to clipboard.
* **Remove / Force Remove**: Clean up the worktree safely. The extension blocks removing the active or main worktrees, and prompts for confirmation if there are uncommitted changes.

![Quick Actions](https://github.com/user-attachments/assets/cd9f5397-efc5-4461-b993-b56a02727ebc)

---

## ⚙️ Settings

Configure the default behaviors under **Git > Settings > Source Control > Git Worktree**:

* **Default Worktree Directory**: Set an absolute path where new worktrees will be created. If left empty, defaults to a sibling `<repo>_Worktrees` folder. *(Ignored when a Worktree Sub-Folder is specified).*
* **Worktree Sub-Folder**: Define a folder created inside the repository itself (e.g. `.worktrees`) to hold worktrees. *(When set, this takes precedence over the Default Worktree Directory).*
* **Preserve Branch Hierarchy**: Toggle whether to preserve the branch folder structure in worktree paths (e.g. `feature/foo` becomes `Worktrees\feature\foo` vs `Worktrees\feature-foo`).

![Settings](https://github.com/user-attachments/assets/4eff89dc-f4b9-402b-8780-db026eb5d708)

---

## 🪵 Logging

All Git execution commands, warnings, and diagnostic messages are cleanly logged to Visual Studio's **Output Window** under the **Git Worktree** pane.

![Logging](https://github.com/user-attachments/assets/d632f0a3-fb3e-4ec0-8f08-7fdff6901941)

---

## 📦 Requirements

* Visual Studio 2022 (17.0 or later) or Visual Studio 2026
* Active Git repository or solution open in Visual Studio

---

## 📄 License

This project is licensed under the MIT License.

---

## 🛠️ Development Setup

Requires the .NET 10 SDK.

```bash
# Clone the repository
git clone https://github.com/MrRazor22/GitWorkTree.git

# Open in Visual Studio
start GitWorkTree.sln

# Build
dotnet build

# Run tests
dotnet test
```

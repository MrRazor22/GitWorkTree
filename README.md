Git Worktree Extension for Visual Studio

A lightweight extension to easily manage Git worktrees inside Visual Studio. Create, switch, and work on multiple branches without leaving your IDE.

What it does:

* Create new worktrees for any branch
* Switch between worktrees smoothly
* Manage multiple branches side-by-side

Why? Visual Studio’s Git support doesn’t handle multi-branch workflows well. This fixes that.

How to install:
Get it from the Visual Studio Marketplace or build from source.

Usage:
After installation, two options appear under **Git > Git Worktree**:

![image](https://github.com/user-attachments/assets/158b87d3-b6a0-4137-bb1d-0c15831db435)


* **Create Worktree** (Ctrl+Shift+=)
* **Manage Worktrees** (Ctrl+Shift+)

**Create Worktree Window:**

![image](https://github.com/user-attachments/assets/105abd78-32bb-456d-9024-58c2152766d4)

1. Shows current repo
2. Choose branch from a list (branches with a "+" prefix are active worktrees)
3. Set path for new worktree
4. Force create option (overwrites overlapping worktrees)
5. Option to open the new worktree in a new Visual Studio window (works if “Load” is enabled in settings)
6. Creates new Worktree for selected branch in selected path

**Manage Worktrees Window:**

![image](https://github.com/user-attachments/assets/178b9b63-61c3-47e8-a2b7-72f80a3372cb)

1. Shows current repo
2. List of all created worktrees
3. Option to open the selected worktree in a new Visual Studio window
4. Open selected worktree
5. Force remove (even if uncommitted changes exist)
6. Prune dangling worktree references
7. Remove selected worktree

**Settings:**

![image](https://github.com/user-attachments/assets/008a34af-1337-4fef-ab34-b91d54249867)

Accessible via **Git > Settings > Source Control > Git Worktree**

* **Default branch path:** If empty, defaults to `repo path\<repo>_worktree\<branch>`, else user-defined path
* **Load:** If enabled, opens new worktree in a new window after creation (enables the checkbox in create window)

This keeps your multi-branch Git workflow smooth and integrated, right inside Visual Studio.

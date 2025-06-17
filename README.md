---

## Git Worktree Extension for Visual Studio

A lightweight extension to easily manage Git worktrees inside Visual Studio. Create, switch, and work on multiple branches without leaving your IDE.

### What is git worktree?

Git worktree lets you check out multiple branches from the same Git repository at the same time, each into its own folder. You can work on different versions or features side by side, no more constant switching and rebuilding.

Instead of juggling one branch at a time, worktrees give each branch its own clean workspace. It’s like having multiple clones, but without wasting disk space or resyncing the whole repo.

This extension brings that power straight into Visual Studio. No command-line. No hassle.

---

## Why you need this extension?

Visual Studio’s Git support doesn’t handle multi-branch workflows well. This extension fixes that.

* No need for a separate Git client just to manage worktrees (Visual Studio already handles most Git features).
* Other extensions (like Git Extensions) require installing their own Git client this doesn’t.
* You **don’t even need Git installed on your PC** this uses Visual Studio’s inbuilt Git.

---

## What it does:

* Create new worktrees for any branch
* Switch between worktrees smoothly
* Manage multiple branches side-by-side

---

## How to install:

Get it from the Visual Studio Marketplace, or build it from source.

---

## Usage:

Open a solution or folder with a Git repository.
**Note:** this extension only works if there’s an active Git repo or solution open otherwise, it won’t show up.

After installation, two options appear under **Git > Git Worktree**:

![image](https://github.com/user-attachments/assets/158b87d3-b6a0-4137-bb1d-0c15831db435)

* **Create Worktree** (`Ctrl+Shift+=`)
* **Manage Worktrees** (`Ctrl+Shift+\\`)

---

### **Create Worktree Window:**

![image](https://github.com/user-attachments/assets/e8186e83-9a7f-498e-ad3b-e92b44fc7447)

1. Shows current repo
2. Choose branch from the dropdown

   * You can also type to filter it will suggest relevant matches
   * It lists all branches in the repo
   * Branches with a "+" prefix already have a worktree
3. Set path for the new worktree

   * By default, the path will be:
     `repo path/repo_name_worktree/branch_name`
   * Worktree name = branch name
   * You can choose your own directory
   * You can also set a default base directory in **Settings** (explained later)
4. **Force create**: overwrites existing/overlapping worktrees
5. Option to **open the new worktree in a new Visual Studio window** (only works if “Load” is enabled in settings)
6. Creates the worktree for the selected branch at the selected path

---

### **Manage Worktrees Window:**

![image](https://github.com/user-attachments/assets/087ff258-f3c6-4120-a048-23ecbdd5e81c)

1. Shows current repo
2. Lists all created worktrees (with their paths)
3. Option to open the selected worktree in a new VS window
4. Open selected worktree
5. **Force remove** (even if there are uncommitted changes)
6. **Prune** dangling worktree references

   * Sometimes when you delete a worktree manually, Git still holds a reference
   * Pruning removes that, so Git allows creating worktree again for that branch
7. Remove selected worktree

---

### **Output Window:**

All Git command executions performed by the extension are logged in Visual Studio’s Output pane (under Git Worktree), and key status updates also appear in the status bar at the bottom for quick feedback.

![image](https://github.com/user-attachments/assets/06c97844-1709-42f4-9658-bfc50914be05)

---

### **Settings:**

![image](https://github.com/user-attachments/assets/db5bab93-cc87-48a1-a46c-b979c7968774)

Accessible via **Git > Settings > Source Control > Git Worktree**

* **Default Worktree Directory**:

  * If empty, defaults to:
    `repo directory\repo_name_worktree\branch_name`
  * Otherwise, uses your custom directory
* **Load**:

  * If True: opens new worktree in a new window after creation
  * If False: it just creates the worktree but doesn’t open it
    Useful if you want to prepare worktrees but open them later

---

This keeps your multi-branch Git workflow clean and fully integrated without leaving Visual Studio.

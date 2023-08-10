package views

import (
	"fmt"
	"log"
	"os"
	"path/filepath"
	"strconv"
	"strings"

	"desktop/controller"
	"desktop/models"
	"desktop/security"

	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

var tree *widgets.QTreeWidget = nil
var group *widgets.QAction = nil
var sub *widgets.QAction = nil
var add *widgets.QAction = nil
var save *widgets.QAction = nil
var sync *widgets.QAction = nil
var table *widgets.QTableWidget = nil
var login *widgets.QAction = nil
var register *widgets.QAction = nil
var settings *widgets.QAction = nil
var logout *widgets.QAction = nil
var masterPassword string = ""
var fileDB string = ""
var asterisk string = "********************"
var user models.User = models.User{}

func CreateMenu() *widgets.QMenuBar {
	menu := widgets.NewQMenuBar(nil)

	file := menu.AddMenu2("File")

	newDatabase := widgets.NewQAction(nil)
	newDatabase.SetIcon(gui.NewQIcon5("icons/database.svg"))
	newDatabase.SetText("New database")
	newDatabase.ConnectTriggered(func(bool) {
		newDbFile()
	})

	openDatabase := widgets.NewQAction(nil)
	openDatabase.SetIcon(gui.NewQIcon5("icons/open.svg"))
	openDatabase.SetText("Open database")
	openDatabase.ConnectTriggered(func(bool) {
		openDb("")
	})

	account := menu.AddMenu2("Account")

	email := widgets.NewQAction(nil)
	email.SetIcon(gui.NewQIcon5("icons/username.svg"))
	email.SetEnabled(false)
	email.SetVisible(false)

	login = widgets.NewQAction(nil)
	login.SetIcon(gui.NewQIcon5("icons/login.svg"))
	login.SetText("Login")
	login.ConnectTriggered(func(bool) {
		emai, token, err := Login()
		if err != nil {
			log.Println(err)
			return
		}
		user = models.User{Email: emai, Token: token}
		controller.GetSettings(&user)
		login.SetEnabled(false)
		register.SetEnabled(false)
		settings.SetEnabled(true)
		sync.SetEnabled(true)
		save.SetEnabled(true)
		sync.SetVisible(true)
		save.SetVisible(true)
		logout.SetEnabled(true)
		logout.SetText("Logout: " + emai)
		email.SetText(emai)
		email.SetVisible(true)
		sure := areYouSure("Do you want to sync to your remote database, if you have one?\n\nYou can always sync later!")
		if sure {
			file := saveFile()
			if file != "" {
				err := controller.Sync(&user, file)
				if err != nil {
					log.Println(err)
					showError("You dont have a remote database yet!")
					return
				}
				openDb(file)
			}
		}
	})

	register = widgets.NewQAction(nil)
	register.SetIcon(gui.NewQIcon5("icons/register.svg"))
	register.SetText("Register")
	register.ConnectTriggered(func(bool) {
		Register()
	})

	separator := widgets.NewQAction(nil)
	separator.SetSeparator(true)

	settings = widgets.NewQAction(nil)
	settings.SetIcon(gui.NewQIcon5("icons/settings.svg"))
	settings.SetText("Settings")
	settings.ConnectTriggered(func(bool) {
		Settings(&user)
	})
	settings.SetEnabled(false)

	separator2 := widgets.NewQAction(nil)
	separator2.SetSeparator(true)

	logout = widgets.NewQAction(nil)
	logout.SetIcon(gui.NewQIcon5("icons/logout.svg"))
	logout.SetText("Logout")
	logout.ConnectTriggered(func(bool) {
		user = models.User{}
		logout.SetEnabled(false)
		logout.SetText("Logout")
		login.SetEnabled(true)
		register.SetEnabled(true)
		settings.SetEnabled(false)
		sync.SetEnabled(false)
		save.SetEnabled(false)
		sync.SetVisible(false)
		save.SetVisible(false)
		email.SetVisible(false)
		tree.Clear()
		table.ClearContents()
		table.SetRowCount(0)
		showInfo("Logout successful!")
	})
	logout.SetEnabled(false)

	account.InsertAction(nil, email)
	account.InsertAction(nil, login)
	account.InsertAction(nil, register)
	account.InsertAction(nil, separator)
	account.InsertAction(nil, settings)
	account.InsertAction(nil, separator2)
	account.InsertAction(nil, logout)

	file.InsertAction(nil, newDatabase)
	file.InsertAction(nil, openDatabase)

	help := menu.AddMenu2("Help")

	update := widgets.NewQAction(nil)
	update.SetIcon(gui.NewQIcon5("icons/refresh.svg"))
	update.SetText("Check for updates")
	update.ConnectTriggered(func(bool) {
		showInfo("Coming soon!")
	})

	help.InsertAction(nil, update)

	about := widgets.NewQAction(nil)
	about.SetIcon(gui.NewQIcon5("icons/about.svg"))
	about.SetText("About")
	about.ConnectTriggered(func(bool) {
		widget := widgets.NewQWidget(nil, 0)
		widget.SetWindowTitle("About")
		widget.SetFixedWidth(300)
		widget.SetFixedHeight(200)
		widget.SetStyleSheet("background-color: #FFFFFF;")
		layout := widgets.NewQVBoxLayout2(widget)
		layout.SetContentsMargins(0, 0, 0, 0)
		layout.SetSpacing(0)
		label := widgets.NewQLabel2("Finalpass", nil, 0)
		label.SetAlignment(core.Qt__AlignCenter)
		label.SetStyleSheet("font-size: 20px; font-weight: bold;")
		label2 := widgets.NewQLabel2("Version 1.0.0", nil, 0)
		label2.SetAlignment(core.Qt__AlignCenter)
		label2.SetStyleSheet("font-size: 16px;")
		label3 := widgets.NewQLabel2("Developed by: Ali Symeri", nil, 0)
		label3.SetAlignment(core.Qt__AlignCenter)
		label3.SetOpenExternalLinks(true)
		label3.SetStyleSheet("font-size: 16px;")
		layout.AddWidget(label, 0, 0)
		layout.AddWidget(label2, 0, 0)
		layout.AddWidget(label3, 0, 0)
		widget.Show()
	})

	help.InsertAction(nil, about)

	return menu
}

func CreateToolBar() *widgets.QToolBar {
	tool := widgets.NewQToolBar2(nil)
	tool.SetIconSize(core.NewQSize2(32, 32))
	tool.SetStyleSheet("background-color: #FFFFFF;")
	tool.SetFixedHeight(50)
	database := widgets.NewQAction(nil)
	database.SetIcon(gui.NewQIcon5("icons/database.svg"))
	database.SetToolTip("New database")
	database.ConnectTriggered(func(bool) {
		newDbFile()
	})

	open := widgets.NewQAction(nil)
	open.SetIcon(gui.NewQIcon5("icons/open.svg"))
	open.SetToolTip("Open database")
	open.ConnectTriggered(func(bool) {
		openDb("")
	})

	sub = widgets.NewQAction(nil)
	sub.SetIcon(gui.NewQIcon5("icons/sub.svg"))
	sub.SetToolTip("Create sub database")
	sub.ConnectTriggered(func(bool) {
		log.Println("Create sub database")
		db := getSubDatabaseName("")
		if db != "" {
			database, err := controller.CreateSubDatabase(fileDB, masterPassword, db)
			if err != nil {
				log.Println(err)
				showError("Failed to add sub database!")
				return
			} else {
				db, err := controller.GetDatabase(fileDB, masterPassword, database.Name)
				if err != nil {
					log.Println(err)
					showError("Failed to get data!")
					return
				}
				parent := widgets.NewQTreeWidgetItem2([]string{db.Name}, 0)
				parent.SetIcon(0, gui.NewQIcon5("icons/sub2.svg"))
				tree.AddTopLevelItem(parent)
				for _, group := range db.SecretGroups {
					child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
					child.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
					parent.AddChild(child)
				}
				parent.SetExpanded(true)
				save.SetEnabled(true)
			}
		}
	})
	sub.SetEnabled(false)

	group = widgets.NewQAction(nil)
	group.SetIcon(gui.NewQIcon5("icons/group.svg"))
	group.SetToolTip("Add new secret group")
	group.ConnectTriggered(func(bool) {
		grp := getSecretGroup("")
		if grp != "" {
			if tree.CurrentItem().Parent().Text(0) == "" {
				grp, err := controller.CreateSecretGroup(fileDB, masterPassword, tree.CurrentItem().Text(0), grp)
				if err != nil {
					log.Println(err)
					showError("Failed to add secret group!")
					return
				} else {
					g := widgets.NewQTreeWidgetItem2([]string{grp.Name}, 0)
					g.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
					tree.CurrentItem().AddChild(g)
					save.SetEnabled(true)
				}
			} else {
				grp, err := controller.CreateSecretGroup(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), grp)
				if err != nil {
					log.Println(err)
					showError("Failed to add secret group!")
					return
				} else {
					g := widgets.NewQTreeWidgetItem2([]string{grp.Name}, 0)
					g.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
					tree.CurrentItem().Parent().AddChild(g)
					save.SetEnabled(true)
				}
			}
		}
	})
	group.SetEnabled(false)

	add = widgets.NewQAction(nil)
	add.SetIcon(gui.NewQIcon5("icons/key.svg"))
	add.SetToolTip("Add new secret")
	add.ConnectTriggered(func(bool) {
		addSecret()
	})
	add.SetEnabled(false)

	save = widgets.NewQAction(nil)
	save.SetIcon(gui.NewQIcon5("icons/save.svg"))
	save.SetToolTip("Save")
	save.ConnectTriggered(func(bool) {
		sure := areYouSure("Are you sure you want to save?\n\nThis will overwrite the remote database with this current one!")
		if !sure {
			return
		}
		err := controller.Save(&user, fileDB)
		if err != nil {
			log.Println(err)
			showError("Failed to save!")
			save.SetEnabled(true)
			return
		}
		save.SetEnabled(false)
	})
	save.SetEnabled(false)
	save.SetVisible(false)

	sync = widgets.NewQAction(nil)
	sync.SetIcon(gui.NewQIcon5("icons/refresh.svg"))
	sync.SetToolTip("Sync")
	sync.ConnectTriggered(func(bool) {
		if fileDB == "" {
			file := saveFile()
			if file != "" {
				err := controller.Sync(&user, file)
				if err != nil {
					log.Println(err)
					showError("Failed to sync!")
					return
				}
				openDb(file)
			}
		} else {
			sure := areYouSure("Are you sure you want to sync?\n\nThis will overwrite your current database!")
			if !sure {
				return
			}
			err := controller.Sync(&user, fileDB)
			if err != nil {
				log.Println(err)
				showError("Failed to sync!")
				return
			}
			openDb(fileDB)
		}
	})
	sync.SetEnabled(false)
	sync.SetVisible(false)

	line := widgets.NewQFrame(nil, 0)
	line.SetFrameShape(widgets.QFrame__VLine)
	line.SetFrameShadow(widgets.QFrame__Sunken)

	line2 := widgets.NewQFrame(nil, 0)
	line2.SetFrameShape(widgets.QFrame__VLine)
	line2.SetFrameShadow(widgets.QFrame__Sunken)

	tool.InsertAction(nil, database)
	tool.InsertAction(nil, open)
	tool.AddWidget(line)
	tool.InsertAction(nil, save)
	tool.InsertAction(nil, sync)
	tool.AddWidget(line2)
	tool.InsertAction(nil, sub)
	tool.InsertAction(nil, group)
	tool.InsertAction(nil, add)

	return tool
}

func CreateSideMenu() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetFixedWidth(100)
	widget.SetMinimumWidth(50)
	widget.SetMaximumWidth(200)

	tree = widgets.NewQTreeWidget(nil)
	tree.SetHeaderHidden(true)
	tree.SetColumnCount(1)
	tree.SetAnimated(true)
	tree.SetUniformRowHeights(true)
	tree.SetItemsExpandable(true)
	tree.SetHorizontalScrollBarPolicy(1)
	tree.SetVerticalScrollBarPolicy(1)
	tree.SetAutoScroll(true)
	tree.SetAutoScrollMargin(10)

	tree.ConnectItemClicked(func(item *widgets.QTreeWidgetItem, column int) {
		table.ClearContents()
		table.SetRowCount(0)
		if item.Parent().Text(0) == "" {
			if item.Child(0).Text(0) != "" {
				secrets, err := controller.GetSecrets(fileDB, masterPassword, item.Text(0), item.Child(0).Text(0))
				if err != nil {
					log.Println(err)
					showError("Failed to get data!")
					return
				}
				for _, secret := range secrets {
					setTableItems(secret)
				}
			}
		} else {
			secrets, err := controller.GetSecrets(fileDB, masterPassword, item.Parent().Text(0), item.Text(0))
			if err != nil {
				log.Println(err)
				showError("Failed to get data!")
				return
			}
			for _, secret := range secrets {
				setTableItems(secret)
			}
		}
	})

	menu := widgets.NewQMenu(nil)

	edit := menu.AddAction("Edit")
	edit.SetIcon(gui.NewQIcon5("icons/edit.svg"))
	edit.ConnectTriggered(func(bool) {
		if tree.CurrentItem().Parent().Text(0) == "" {
			name := getSubDatabaseName(tree.CurrentItem().Text(0))
			if name != "" {
				d, err := controller.UpdateDatabase(fileDB, masterPassword, tree.CurrentItem().Text(0), name)
				if err != nil {
					log.Println(err)
					showError("Failed to update database!")
					return
				} else {
					tree.CurrentItem().SetText(0, d.Name)
					save.SetEnabled(true)
				}
			}
		} else {
			group := getSecretGroup(tree.CurrentItem().Text(0))
			if group != "" {
				g, err := controller.UpdateSecretGroup(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), group)
				if err != nil {
					log.Println(err)
					showError("Failed to update secret group!")
					return
				} else {
					tree.CurrentItem().SetText(0, g.Name)
					save.SetEnabled(true)
				}
			}
		}
	})

	delete := menu.AddAction("Delete")
	delete.SetIcon(gui.NewQIcon5("icons/delete.svg"))
	delete.ConnectTriggered(func(bool) {
		if tree.CurrentItem().Parent().Text(0) == "" {
			if tree.CurrentItem().Child(0).Text(0) != "" {
				showError("Delete all sub databases first!")
			} else {
				err := controller.DeleteDatabase(fileDB, masterPassword, tree.CurrentItem().Text(0))
				if err != nil {
					log.Println(err)
					showError("Failed to delete database!")
					return
				} else {
					tree.Clear()
					table.ClearContents()
					table.SetRowCount(0)
					group.SetEnabled(false)
					add.SetEnabled(false)
					save.SetEnabled(true)
					sub.SetEnabled(false)
				}
			}
		} else {
			group, err := controller.GetSecretGroup(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0))
			if err != nil {
				log.Println(err)
				showError("Failed to delete secret group!")
				return
			}
			err = controller.DeleteSecretGroup(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), group.Name)
			if err != nil {
				log.Println(err)
				showError("Failed to delete secret group!")
				return
			} else {
				tree.CurrentItem().Parent().RemoveChild(tree.CurrentItem())
			}
		}
	})

	tree.SetContextMenuPolicy(core.Qt__CustomContextMenu)

	tree.ConnectCustomContextMenuRequested(func(pos *core.QPoint) {
		if tree.TopLevelItemCount() == 0 {
			return
		}
		menu.Exec2(tree.MapToGlobal(pos), nil)
	})

	layout := widgets.NewQHBoxLayout2(widget)
	layout.SetContentsMargins(0, 0, 0, 0)
	layout.SetSpacing(0)
	layout.AddWidget(tree, 0, 0)

	return widget
}

func CreateMain() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetStyleSheet("background-color: #FFFFFF;")

	table = widgets.NewQTableWidget(nil)
	table.SetColumnCount(8)
	table.SetRowCount(0)
	table.SetHorizontalHeaderLabels([]string{"ID", "Title", "Username", "Password", "URL", "Description", "Created At", "Updated At"})
	table.SetEditTriggers(widgets.QAbstractItemView__NoEditTriggers)
	table.SetSelectionBehavior(widgets.QAbstractItemView__SelectRows)
	table.SetSelectionMode(widgets.QAbstractItemView__SingleSelection)
	table.SetShowGrid(true)
	table.SetHorizontalScrollBarPolicy(1)
	table.SetVerticalScrollBarPolicy(1)
	table.SetAutoScroll(true)
	table.SetAutoScrollMargin(10)
	table.SetColumnHidden(0, true)
	table.VerticalHeader().SetVisible(false)
	table.SetAlternatingRowColors(true)
	table.SetStyleSheet("alternate-background-color: #d1dce0;")

	table.ConnectCellDoubleClicked(func(row int, column int) {
		id := table.Item(row, 0).Text()
		integer, err := strconv.Atoi(id)
		if err != nil {
			log.Println(err)
			showError("Failed to update secret!")
			return
		}
		if tree.CurrentItem().Parent().Text(0) == "" {
			if tree.CurrentItem().Child(0).Text(0) != "" {
				s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer)
				if err != nil {
					log.Println(err)
					showError("Failed to update secret!")
					return
				}
				secret := getSecret(s)
				if secret.Username == "" && secret.Password == nil {
					return
				}
				sct, err := controller.UpdateSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer, secret)
				if err != nil {
					log.Println(err)
					showError("Failed to update secret!")
					return
				} else {
					setTableItems2(row, sct)
				}
			}
		} else {
			s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer)
			if err != nil {
				log.Println(err)
				showError("Failed to update secret!")
				return
			}
			secret := getSecret(s)
			if secret.Username == "" && secret.Password == nil {
				return
			}
			sct, err := controller.UpdateSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer, secret)
			if err != nil {
				log.Println(err)
				showError("Failed to update secret!")
				return
			} else {
				setTableItems2(row, sct)
			}
		}
	})

	menu := widgets.NewQMenu(nil)

	copyUsername := menu.AddAction("Copy username")
	copyUsername.SetIcon(gui.NewQIcon5("icons/username.svg"))
	copyUsername.ConnectTriggered(func(bool) {
		row := table.CurrentRow()
		username := table.Item(row, 2).Text()
		if username != "" {
			clipboard := gui.QGuiApplication_Clipboard()
			clipboard.SetText(username, gui.QClipboard__Clipboard)
		}
	})

	copyPassword := menu.AddAction("Copy password")
	copyPassword.SetIcon(gui.NewQIcon5("icons/password.svg"))
	copyPassword.ConnectTriggered(func(bool) {
		row := table.CurrentRow()
		id := table.Item(row, 0).Text()
		integer, err := strconv.Atoi(id)
		if err != nil {
			log.Println(err)
			showError("Failed to copy password!")
			return
		}
		if tree.CurrentItem().Parent().Text(0) == "" {
			if tree.CurrentItem().Child(0).Text(0) != "" {
				s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer)
				if err != nil {
					log.Println(err)
					showError("Failed to copy password!")
					return
				}
				clipboard := gui.QGuiApplication_Clipboard()
				clipboard.SetText(string(s.Password), gui.QClipboard__Clipboard)
			}
		} else {
			s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer)
			if err != nil {
				log.Println(err)
				showError("Failed to copy password!")
				return
			}
			clipboard := gui.QGuiApplication_Clipboard()
			clipboard.SetText(string(s.Password), gui.QClipboard__Clipboard)
		}
	})

	separator := widgets.NewQAction(nil)
	separator.SetSeparator(true)
	menu.InsertAction(nil, separator)

	edit := menu.AddAction("Edit")
	edit.SetIcon(gui.NewQIcon5("icons/edit.svg"))
	edit.ConnectTriggered(func(bool) {
		row := table.CurrentRow()
		id := table.Item(row, 0).Text()
		integer, err := strconv.Atoi(id)
		if err != nil {
			log.Println(err)
			showError("Failed to update secret!")
			return
		}
		if tree.CurrentItem().Parent().Text(0) == "" {
			if tree.CurrentItem().Child(0).Text(0) != "" {
				s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer)
				if err != nil {
					log.Println(err)
					showError("Failed to update secret!")
					return
				}
				secret := getSecret(s)
				if secret.Username == "" && secret.Password == nil {
					return
				}
				sct, err := controller.UpdateSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer, secret)
				if err != nil {
					log.Println(err)
					showError("Failed to update secret!")
					return
				} else {
					setTableItems2(row, sct)
				}
			}
		} else {
			s, err := controller.GetSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer)
			if err != nil {
				log.Println(err)
				showError("Failed to update secret!")
				return
			}
			secret := getSecret(s)
			if secret.Username == "" && secret.Password == nil {
				return
			}
			sct, err := controller.UpdateSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer, secret)
			if err != nil {
				log.Println(err)
				showError("Failed to update secret!")
				return
			} else {
				setTableItems2(row, sct)
			}
		}
	})

	delete := menu.AddAction("Delete")
	delete.SetIcon(gui.NewQIcon5("icons/delete.svg"))
	delete.ConnectTriggered(func(bool) {
		row := table.CurrentRow()
		id := table.Item(row, 0).Text()
		integer, err := strconv.Atoi(id)
		if err != nil {
			log.Println(err)
			showError("Failed to delete secret!")
			return
		}
		if tree.CurrentItem().Parent().Text(0) == "" {
			if tree.CurrentItem().Child(0).Text(0) != "" {
				err := controller.DeleteSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), integer)
				if err != nil {
					log.Println(err)
					showError("Failed to delete secret!")
					return
				}
				table.RemoveRow(row)
				save.SetEnabled(true)
			}
		} else {
			err := controller.DeleteSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), integer)
			if err != nil {
				log.Println(err)
				showError("Failed to delete secret!")
				return
			}
			table.RemoveRow(row)
			save.SetEnabled(true)
		}
	})

	separator2 := widgets.NewQAction(nil)
	separator2.SetSeparator(true)
	menu.InsertAction(nil, separator2)

	add := menu.AddAction("Add new secret")
	add.SetIcon(gui.NewQIcon5("icons/key.svg"))
	add.ConnectTriggered(func(bool) {
		addSecret()
	})

	table.SetContextMenuPolicy(core.Qt__CustomContextMenu)

	table.ConnectCustomContextMenuRequested(func(pos *core.QPoint) {
		if table.RowCount() == 0 {
			return
		}
		row := table.CurrentRow()
		if row < 0 {
			table.ClearSelection()
			return
		}
		menu.Exec2(table.MapToGlobal(pos), nil)
	})

	layout := widgets.NewQHBoxLayout2(widget)
	layout.SetContentsMargins(0, 0, 0, 0)
	layout.SetSpacing(0)
	layout.AddWidget(table, 0, 0)

	return widget
}

func createPassword(file string) string {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Create master password")

	layout := widgets.NewQVBoxLayout2(dialog)

	horizontalLayout := widgets.NewQHBoxLayout2(nil)

	formLayout := widgets.NewQFormLayout(nil)

	label := widgets.NewQLabel(nil, 0)
	label.SetText(fmt.Sprintf("Database: %s\n\nSpecify a new master password, which will be used to encrypt the database.\n\nRemember the master password that you enter, \nif you lose it you will not be able to open the database.", file))

	passwordField := widgets.NewQLineEdit(nil)
	repeatField := widgets.NewQLineEdit(nil)

	passwordField.SetEchoMode(2)

	repeatField.SetEchoMode(2)

	passwordField.ConnectTextChanged(func(_ string) {
		if passwordField.Text() != repeatField.Text() {
			passwordField.SetStyleSheet("border: 1px solid red")
			repeatField.SetStyleSheet("border: 1px solid red")
		} else {
			passwordField.SetStyleSheet("border: 1px solid green")
			repeatField.SetStyleSheet("border: 1px solid green")
		}
	})

	repeatField.ConnectTextChanged(func(_ string) {
		if passwordField.Text() != repeatField.Text() {
			passwordField.SetStyleSheet("border: 1px solid red")
			repeatField.SetStyleSheet("border: 1px solid red")
		} else {
			passwordField.SetStyleSheet("border: 1px solid green")
			repeatField.SetStyleSheet("border: 1px solid green")
		}
	})

	formLayout.AddRow5(label)
	formLayout.AddRow3("Master password:", passwordField)
	formLayout.AddRow3("Repeat password:", repeatField)

	formLayout2 := widgets.NewQFormLayout(nil)

	sh := gui.NewQIcon5("icons/show.svg")
	show := widgets.NewQPushButton3(sh, "", nil)
	show.SetStyleSheet("border-width: 0px;")
	show.ConnectClicked(func(bool) {
		if passwordField.EchoMode() == 2 {
			passwordField.SetEchoMode(0)
			repeatField.SetEchoMode(0)
			sh = gui.NewQIcon5("icons/dontshow.svg")
			show.SetIcon(sh)
		} else {
			passwordField.SetEchoMode(2)
			repeatField.SetEchoMode(2)
			sh = gui.NewQIcon5("icons/show.svg")
			show.SetIcon(sh)
		}
	})

	button := widgets.NewQPushButton3(gui.NewQIcon5("icons/refresh.svg"), "", nil)
	button.SetStyleSheet("border-width: 0px;")

	password := ""

	button.ConnectClicked(func(bool) {
		password = security.GenerateStrongPassword(20)
		passwordField.SetText(password)
		repeatField.SetText(password)
		passwordField.SetStyleSheet("border: 1px solid green")
		repeatField.SetStyleSheet("border: 1px solid green")
	})

	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(show)
	formLayout2.AddRow5(button)

	horizontalLayout.AddLayout(formLayout, 0)
	horizontalLayout.AddLayout(formLayout2, 0)

	layout.AddLayout(horizontalLayout, 0)

	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok | widgets.QDialogButtonBox__Cancel)
	buttons.ConnectAccepted(func() {
		if passwordField.Text() == repeatField.Text() {
			dialog.Accept()
		} else {
			showError("Passwords do not match!")
		}
	})
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)

	dialog.SetModal(true)
	dialog.Show()

	if dialog.Exec() == int(widgets.QDialog__Accepted) {
		return passwordField.Text()
	}
	return ""
}

func getPassword(file string) string {
	dialog := widgets.NewQInputDialog(nil, 0)
	dialog.SetWindowTitle("Enter master password")
	dialog.SetLabelText(fmt.Sprintf("Database: %s\n\nEnter master password to open database.", file))
	dialog.SetOkButtonText("Ok")
	dialog.SetCancelButtonText("Cancel")
	dialog.SetTextEchoMode(2)
	dialog.SetInputMode(widgets.QInputDialog__TextInput)

	checkbox := widgets.NewQCheckBox(dialog)
	checkbox.SetText("Show Password")
	checkbox.SetChecked(false)
	checkbox.ConnectStateChanged(func(state int) {
		if state == 2 {
			dialog.SetTextEchoMode(0)
		} else {
			dialog.SetTextEchoMode(2)
		}
	})
	dialog.Layout().AddWidget(checkbox)

	dialog.SetModal(true)
	dialog.Show()
	if dialog.Exec() == 1 {
		return dialog.TextValue()
	}
	return ""
}

func newDb() bool {
	dialog := widgets.NewQMessageBox(nil)
	dialog.SetWindowTitle("New database")
	dialog.SetText("Your data will be stored in a regular file.\n\nAfter clicking OK, you will be asked to choose a location where the file should be saved.")
	dialog.SetIcon(widgets.QMessageBox__Information)
	dialog.SetStandardButtons(widgets.QMessageBox__Ok | widgets.QMessageBox__Cancel)
	dialog.SetDefaultButton2(widgets.QMessageBox__Ok)
	dialog.SetEscapeButton2(widgets.QMessageBox__Cancel)
	dialog.SetModal(true)
	dialog.Show()
	return dialog.Exec() == int(widgets.QMessageBox__Ok)
}

func getSubDatabaseName(name string) string {
	dialog := widgets.NewQInputDialog(nil, 0)
	dialog.SetWindowTitle("Create sub database")
	dialog.SetLabelText("Specify a new name for the sub database.")
	dialog.SetOkButtonText("Ok")
	dialog.SetCancelButtonText("Cancel")
	dialog.SetTextEchoMode(0)
	dialog.SetInputMode(widgets.QInputDialog__TextInput)
	dialog.SetModal(true)

	if name != "" {
		dialog.SetWindowTitle("Edit sub database")
		dialog.SetLabelText("Specify a new name for the sub database.")
		dialog.SetTextValue(name)
	}

	dialog.Show()
	if dialog.Exec() == 1 {
		return dialog.TextValue()
	}
	return ""
}

func getSecretGroup(group string) string {
	dialog := widgets.NewQInputDialog(nil, 0)
	dialog.SetWindowTitle("Create secret group")
	dialog.SetLabelText("Specify a name for the secret group.")
	dialog.SetOkButtonText("Ok")
	dialog.SetCancelButtonText("Cancel")
	dialog.SetTextEchoMode(0)
	dialog.SetInputMode(widgets.QInputDialog__TextInput)
	dialog.SetModal(true)

	if group != "" {
		dialog.SetWindowTitle("Edit secret group")
		dialog.SetLabelText("Specify a new name for the secret group.")
		dialog.SetTextValue(group)
	}

	dialog.Show()
	if dialog.Exec() == 1 {
		return dialog.TextValue()
	}
	return ""
}

func getSecret(secret models.Secret) models.Secret {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Create secret")

	layout := widgets.NewQVBoxLayout2(dialog)

	horizontalLayout := widgets.NewQHBoxLayout2(nil)

	formLayout := widgets.NewQFormLayout(nil)

	password := security.GenerateStrongPassword(20)

	titleField := widgets.NewQLineEdit(nil)
	usernameField := widgets.NewQLineEdit(nil)
	passwordField := widgets.NewQLineEdit(nil)
	repeatField := widgets.NewQLineEdit(nil)
	urlField := widgets.NewQLineEdit(nil)
	descriptionField := widgets.NewQTextEdit(nil)
	createdField := widgets.NewQLineEdit(nil)
	updatedField := widgets.NewQLineEdit(nil)

	usernameField.SetStyleSheet("border: 1px solid red")

	passwordField.SetText(password)
	passwordField.SetEchoMode(2)
	passwordField.SetStyleSheet("border: 1px solid green")

	repeatField.SetText(password)
	repeatField.SetEchoMode(2)
	repeatField.SetStyleSheet("border: 1px solid green")

	createdField.SetReadOnly(true)
	updatedField.SetReadOnly(true)

	usernameField.ConnectTextChanged(func(_ string) {
		if usernameField.Text() != "" {
			usernameField.SetStyleSheet("border: 1px solid green")
		} else {
			usernameField.SetStyleSheet("border: 1px solid red")
		}
	})

	passwordField.ConnectTextChanged(func(_ string) {
		if passwordField.EchoMode() == 0 {
			repeatField.SetText(passwordField.Text())
		}
		if passwordField.Text() != repeatField.Text() {
			passwordField.SetStyleSheet("border: 1px solid red")
			repeatField.SetStyleSheet("border: 1px solid red")
		} else {
			passwordField.SetStyleSheet("border: 1px solid green")
			repeatField.SetStyleSheet("border: 1px solid green")
		}
	})

	repeatField.ConnectTextChanged(func(_ string) {
		if passwordField.Text() != repeatField.Text() {
			passwordField.SetStyleSheet("border: 1px solid red")
			repeatField.SetStyleSheet("border: 1px solid red")
		} else {
			passwordField.SetStyleSheet("border: 1px solid green")
			repeatField.SetStyleSheet("border: 1px solid green")
		}
	})

	formLayout.AddRow3("Title:", titleField)
	formLayout.AddRow3("Username:", usernameField)
	formLayout.AddRow3("Password:", passwordField)
	formLayout.AddRow3("Repeat password:", repeatField)
	formLayout.AddRow3("URL:", urlField)
	formLayout.AddRow3("Description:", descriptionField)

	if secret.Username != "" && secret.Password != nil {
		dialog.SetWindowTitle("Edit secret")
		titleField.SetText(secret.Title)
		usernameField.SetText(secret.Username)
		passwordField.SetText(string(secret.Password))
		repeatField.SetText(string(secret.Password))
		urlField.SetText(secret.URL)
		descriptionField.SetText(secret.Description)
		createdField.SetText(secret.CreatedAt.Format("2006-01-02 15:04:05"))
		updatedField.SetText(secret.UpdatedAt.Format("2006-01-02 15:04:05"))
		if usernameField.Text() != "" {
			usernameField.SetStyleSheet("border: 1px solid green")
		} else {
			usernameField.SetStyleSheet("border: 1px solid red")
		}
		formLayout.AddRow3("Created at:", createdField)
		formLayout.AddRow3("Updated at:", updatedField)
	}

	formLayout2 := widgets.NewQFormLayout(nil)

	sh := gui.NewQIcon5("icons/show.svg")
	show := widgets.NewQPushButton3(sh, "", nil)
	show.SetStyleSheet("border-width: 0px;")
	show.ConnectClicked(func(bool) {
		if passwordField.EchoMode() == 2 {
			passwordField.SetEchoMode(0)
			repeatField.SetEchoMode(0)
			repeatField.SetText(passwordField.Text())
			repeatField.SetDisabled(true)
			sh = gui.NewQIcon5("icons/dontshow.svg")
			show.SetIcon(sh)
		} else {
			passwordField.SetEchoMode(2)
			repeatField.SetEchoMode(2)
			repeatField.SetDisabled(false)
			sh = gui.NewQIcon5("icons/show.svg")
			show.SetIcon(sh)
		}
	})

	button := widgets.NewQPushButton3(gui.NewQIcon5("icons/refresh.svg"), "", nil)
	button.SetStyleSheet("border-width: 0px;")

	button.ConnectClicked(func(bool) {
		password = security.GenerateStrongPassword(20)
		passwordField.SetText(password)
		repeatField.SetText(password)
		passwordField.SetStyleSheet("border: 1px solid green")
		repeatField.SetStyleSheet("border: 1px solid green")
	})

	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(widgets.NewQWidget(nil, 0))
	formLayout2.AddRow5(show)
	formLayout2.AddRow5(button)

	horizontalLayout.AddLayout(formLayout, 0)
	horizontalLayout.AddLayout(formLayout2, 0)

	layout.AddLayout(horizontalLayout, 0)

	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok | widgets.QDialogButtonBox__Cancel)
	buttons.ConnectAccepted(func() {
		if passwordField.Text() == repeatField.Text() && usernameField.Text() != "" {
			dialog.Accept()
		} else {
			showError("Missing username or passwords do not match!")
		}
	})
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)

	dialog.SetModal(true)
	dialog.Show()

	if dialog.Exec() == int(widgets.QDialog__Accepted) {
		return models.Secret{
			Title:       titleField.Text(),
			Username:    usernameField.Text(),
			Password:    []byte(passwordField.Text()),
			URL:         urlField.Text(),
			Description: descriptionField.ToPlainText(),
		}
	}
	return models.Secret{}
}

func saveFile() string {
	dialog := widgets.NewQFileDialog(nil, 0)
	file := dialog.GetSaveFileName(nil, "Create new database", "", "Database (*.db)", "", 0)
	return file
}

func loadFile() string {
	dialog := widgets.NewQFileDialog(nil, 0)
	file := dialog.GetOpenFileName(nil, "Open database", "", "Database (*.db)", "", 0)
	return file
}

func showError(message string) {
	dialog := widgets.NewQMessageBox(nil)
	dialog.SetWindowTitle("Error")
	dialog.SetText(message)
	dialog.SetIcon(widgets.QMessageBox__Critical)
	dialog.SetStandardButtons(widgets.QMessageBox__Ok)
	dialog.SetDefaultButton2(widgets.QMessageBox__Ok)
	dialog.SetEscapeButton2(widgets.QMessageBox__Ok)
	dialog.SetModal(true)
	dialog.Show()
	dialog.Exec()
}

func showInfo(message string) {
	dialog := widgets.NewQMessageBox(nil)
	dialog.SetWindowTitle("Info")
	dialog.SetText(message)
	dialog.SetIcon(widgets.QMessageBox__Information)
	dialog.SetStandardButtons(widgets.QMessageBox__Ok)
	dialog.SetDefaultButton2(widgets.QMessageBox__Ok)
	dialog.SetEscapeButton2(widgets.QMessageBox__Ok)
	dialog.SetModal(true)
	dialog.Show()
	dialog.Exec()
}

func areYouSure(message string) bool {
	dialog := widgets.NewQMessageBox(nil)
	dialog.SetWindowTitle("Are you sure?")
	dialog.SetText(message)
	dialog.SetIcon(widgets.QMessageBox__Warning)
	dialog.SetStandardButtons(widgets.QMessageBox__Yes | widgets.QMessageBox__No)
	dialog.SetDefaultButton2(widgets.QMessageBox__No)
	dialog.SetEscapeButton2(widgets.QMessageBox__No)
	dialog.SetModal(true)
	dialog.Show()
	return dialog.Exec() == int(widgets.QMessageBox__Yes)
}

func openDb(f string) {
	file := ""
	if f != "" {
		file = f
	} else {
		file = loadFile()
	}
	if file != "" && controller.CheckFileExist(file) {
		databases := []models.Database{}
		var err error
		password := ""
		for i := 0; i < 3; i++ {
			password = getPassword(file)
			if password != "" {
				databases, err = controller.GetAllDatabases(file, password)
				if err != nil {
					log.Println(err)
					showError("Wrong password!")
					if i == 2 {
						return
					}
				} else {
					break
				}
			} else {
				return
			}
		}
		tree.Clear()
		table.ClearContents()
		table.SetRowCount(0)
		for _, database := range databases {
			parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
			parent.SetIcon(0, gui.NewQIcon5("icons/sub2.svg"))
			tree.AddTopLevelItem(parent)
			for i, group := range database.SecretGroups {
				child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
				child.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
				parent.AddChild(child)
				if i == 0 {
					tree.SetCurrentItem(child)
					for _, secret := range group.Secrets {
						setTableItems(secret)
					}
				}
			}
			parent.SetExpanded(true)
		}
		group.SetEnabled(true)
		add.SetEnabled(true)
		sub.SetEnabled(true)
		masterPassword = password
		fileDB = file
		err2 := controller.WriteConfig(fileDB)
		if err2 != nil {
			log.Println(err2)
			return
		}
	}
}

func newDbFile() {
	db := newDb()
	if db {
		file := saveFile()
		if file != "" && !controller.CheckFileExist(file) {
			name := filepath.Base(file)
			name2 := strings.TrimSuffix(name, filepath.Ext(name))
			password := createPassword(file)
			if password != "" {
				init := controller.InitDB(file, password)
				if init != nil {
					log.Println(init)
					showError("Failed to init database!")
					return
				}
				create := controller.CreateDatabaseAndSecretGroupIfNotExist(file, password, name2)
				if create != nil {
					log.Println(create)
					showError("Failed to create database!")
					return
				}
				databases, err := controller.GetAllDatabases(file, password)
				log.Println(databases)
				if err != nil {
					log.Println(err)
					showError("Failed to get data!")
					return
				}
				tree.Clear()
				table.ClearContents()
				table.SetRowCount(0)
				for _, database := range databases {
					parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
					parent.SetIcon(0, gui.NewQIcon5("icons/sub2.svg"))
					tree.AddTopLevelItem(parent)
					for _, group := range database.SecretGroups {
						child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
						child.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
						parent.AddChild(child)
					}
					parent.SetExpanded(true)
				}
				group.SetEnabled(true)
				add.SetEnabled(true)
				sub.SetEnabled(true)
				masterPassword = password
				fileDB = file
				err2 := controller.WriteConfig(fileDB)
				if err2 != nil {
					log.Println(err2)
					return
				}
			}
		}
	}
}

func addSecret() {
	secret := getSecret(models.Secret{})
	if secret.Username == "" && secret.Password == nil {
		return
	}
	if tree.CurrentItem().Parent().Text(0) == "" {
		if tree.CurrentItem().Child(0).Text(0) != "" {
			sct, err := controller.CreateSecret(fileDB, masterPassword, tree.CurrentItem().Text(0), tree.CurrentItem().Child(0).Text(0), secret)
			if err != nil {
				log.Println(err)
				showError("Failed to add secret!")
				return
			} else {
				setTableItems(sct)
				save.SetEnabled(true)
			}
		}
	} else {
		sct, err := controller.CreateSecret(fileDB, masterPassword, tree.CurrentItem().Parent().Text(0), tree.CurrentItem().Text(0), secret)
		if err != nil {
			log.Println(err)
			showError("Failed to add secret!")
			return
		} else {
			setTableItems(sct)
			save.SetEnabled(true)
		}
	}
}

func setTableItems(secret models.Secret) {
	row := table.RowCount()
	table.InsertRow(row)
	title := widgets.NewQTableWidgetItem2(secret.Title, 0)
	title.SetIcon(gui.NewQIcon5("icons/key.svg"))
	table.SetItem(row, 0, widgets.NewQTableWidgetItem2(fmt.Sprint(secret.ID), 0))
	table.SetItem(row, 1, title)
	table.SetItem(row, 2, widgets.NewQTableWidgetItem2(secret.Username, 0))
	table.SetItem(row, 3, widgets.NewQTableWidgetItem2(asterisk, 0))
	table.SetItem(row, 4, widgets.NewQTableWidgetItem2(secret.URL, 0))
	table.SetItem(row, 5, widgets.NewQTableWidgetItem2(secret.Description, 0))
	table.SetItem(row, 6, widgets.NewQTableWidgetItem2(secret.CreatedAt.Format("2006-01-02 15:04:05"), 0))
	table.SetItem(row, 7, widgets.NewQTableWidgetItem2(secret.UpdatedAt.Format("2006-01-02 15:04:05"), 0))
}

func setTableItems2(row int, secret models.Secret) {
	title := widgets.NewQTableWidgetItem2(secret.Title, 0)
	title.SetIcon(gui.NewQIcon5("icons/key.svg"))
	table.SetItem(row, 0, widgets.NewQTableWidgetItem2(fmt.Sprint(secret.ID), 0))
	table.SetItem(row, 1, title)
	table.SetItem(row, 2, widgets.NewQTableWidgetItem2(secret.Username, 0))
	table.SetItem(row, 3, widgets.NewQTableWidgetItem2(asterisk, 0))
	table.SetItem(row, 4, widgets.NewQTableWidgetItem2(secret.URL, 0))
	table.SetItem(row, 5, widgets.NewQTableWidgetItem2(secret.Description, 0))
	table.SetItem(row, 6, widgets.NewQTableWidgetItem2(secret.CreatedAt.Format("2006-01-02 15:04:05"), 0))
	table.SetItem(row, 7, widgets.NewQTableWidgetItem2(secret.UpdatedAt.Format("2006-01-02 15:04:05"), 0))
	save.SetEnabled(true)
}

func Inits() {
	config := controller.ReadConfig()
	if config.Database == "" {
		log.Println("No database specified in config.json")
		return
	}
	file := config.Database
	if _, err := os.Stat(file); os.IsNotExist(err) {
		log.Println("Database file does not exist")
		err2 := controller.WriteConfig("")
		if err2 != nil {
			log.Println(err2)
			return
		}
		return
	}
	databases := []models.Database{}
	var err error
	password := ""
	for i := 0; i < 3; i++ {
		password = getPassword(file)
		if password != "" {
			databases, err = controller.GetAllDatabases(file, password)
			if err != nil {
				log.Println(err)
				showError("Wrong password!")
				if i == 2 {
					return
				}
			} else {
				break
			}
		} else {
			return
		}
	}
	for _, database := range databases {
		parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
		parent.SetIcon(0, gui.NewQIcon5("icons/sub2.svg"))
		tree.AddTopLevelItem(parent)
		for i, group := range database.SecretGroups {
			child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
			child.SetIcon(0, gui.NewQIcon5("icons/group2.svg"))
			parent.AddChild(child)
			if i == 0 {
				tree.SetCurrentItem(child)
				for _, secret := range group.Secrets {
					setTableItems(secret)
				}
			}
		}
		parent.SetExpanded(true)
	}
	group.SetEnabled(true)
	add.SetEnabled(true)
	sub.SetEnabled(true)
	masterPassword = password
	fileDB = file
}

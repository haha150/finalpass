package main

import (
	"fmt"
	"log"
	"os"
	"path/filepath"
	"strings"

	"password-manager/controller"

	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

var tree *widgets.QTreeWidget = nil
var group *widgets.QAction = nil
var sub *widgets.QAction = nil
var add *widgets.QAction = nil
var save *widgets.QAction = nil
var fileDB string = ""

func createMenu() *widgets.QMenuBar {
	menu := widgets.NewQMenuBar(nil)
	file := menu.AddMenu2("File")

	newDatabase := widgets.NewQAction(nil)
	newDatabase.SetIcon(gui.NewQIcon5("icons/database.svg"))
	newDatabase.SetText("New database")
	newDatabase.ConnectTriggered(func(bool) {
		fmt.Println("New database")
		// controller.CreateDatabase("test")
	})

	openDatabase := widgets.NewQAction(nil)
	openDatabase.SetIcon(gui.NewQIcon5("icons/open.svg"))
	openDatabase.SetText("Open database")
	openDatabase.ConnectTriggered(func(bool) {
		fmt.Println("Open database")
	})

	file.InsertAction(nil, newDatabase)
	file.InsertAction(nil, openDatabase)

	return menu
}

func createToolBar() *widgets.QToolBar {
	tool := widgets.NewQToolBar2(nil)
	tool.SetIconSize(core.NewQSize2(32, 32))
	tool.SetStyleSheet("background-color: #FFFFFF;")
	tool.SetFixedHeight(50)
	database := widgets.NewQAction(nil)
	database.SetIcon(gui.NewQIcon5("icons/database.svg"))
	database.SetToolTip("New database")
	database.ConnectTriggered(func(bool) {
		log.Println("New database")
		db := newDb()
		if db {
			file := saveFile()
			if file != "" && !controller.CheckFileExist(file) {
				name := filepath.Base(file)
				name2 := strings.TrimSuffix(name, filepath.Ext(name))
				password := getPassword(file)
				if password != "" {
					init := controller.InitDB(file)
					if init != nil {
						log.Println(init)
						showError("Failed to init database!")
						return
					}
					create := controller.CreateDatabaseAndSecretGroupIfNotExist(file, name2)
					if create != nil {
						log.Println(create)
						showError("Failed to create database!")
						return
					}
					databases, err := controller.GetAllDatabases(file)
					if err != nil {
						log.Println(err)
						showError("Failed to get data!")
						return
					}
					tree.Clear()
					for _, database := range databases {
						parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
						parent.SetIcon(0, gui.NewQIcon5("icons/database.svg"))
						tree.AddTopLevelItem(parent)
						for _, group := range database.SecretGroups {
							child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
							child.SetIcon(0, gui.NewQIcon5("icons/group.svg"))
							parent.AddChild(child)
						}
						parent.SetExpanded(true)
					}
					group.SetEnabled(true)
					add.SetEnabled(true)
					save.SetEnabled(true)
					sub.SetEnabled(true)
					fileDB = file
				}
			} else {
				showError("Database already exist!")
			}
		} else {
			showError("Failed to create database!")
		}
	})

	open := widgets.NewQAction(nil)
	open.SetIcon(gui.NewQIcon5("icons/open.svg"))
	open.SetToolTip("Open database")
	open.ConnectTriggered(func(bool) {
		log.Println("Open database")
		file := loadFile()
		if file != "" {
			databases, err := controller.GetAllDatabases(file)
			if err != nil {
				log.Println(err)
				showError("Failed to get data!")
				return
			}
			tree.Clear()
			for _, database := range databases {
				parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
				parent.SetIcon(0, gui.NewQIcon5("icons/database.svg"))
				tree.AddTopLevelItem(parent)
				for _, group := range database.SecretGroups {
					child := widgets.NewQTreeWidgetItem2([]string{group.Name}, 0)
					child.SetIcon(0, gui.NewQIcon5("icons/group.svg"))
					parent.AddChild(child)
				}
				parent.SetExpanded(true)
			}
			group.SetEnabled(true)
			add.SetEnabled(true)
			save.SetEnabled(true)
			sub.SetEnabled(true)
			fileDB = file
		} else {
			showError("Failed to open database!")
		}
	})

	sub = widgets.NewQAction(nil)
	sub.SetIcon(gui.NewQIcon5("icons/sub.png"))
	sub.SetToolTip("Create sub database")
	sub.ConnectTriggered(func(bool) {
		log.Println("Create sub database")

	})
	sub.SetEnabled(false)

	group = widgets.NewQAction(nil)
	group.SetIcon(gui.NewQIcon5("icons/group.svg"))
	group.SetToolTip("Add new secret group")
	group.ConnectTriggered(func(bool) {
		log.Println("New secret group")
		log.Println(tree.CurrentItem().Text(0))
		log.Println(tree.CurrentItem().Parent().Text(0))
		if tree.CurrentItem().Parent().Text(0) == "" {
			grp, err := controller.CreateSecretGroup(fileDB, tree.CurrentItem().Text(0), "abcd")
			if err != nil {
				log.Println(err)
				showError("Failed to add secret group!")
				return
			} else {
				tree.CurrentItem().AddChild(widgets.NewQTreeWidgetItem2([]string{grp.Name}, 0))
			}
		} else {
			grp, err := controller.CreateSecretGroup(fileDB, tree.CurrentItem().Parent().Text(0), "abcd2")
			if err != nil {
				log.Println(err)
				showError("Failed to add secret group!")
				return
			} else {
				tree.CurrentItem().Parent().AddChild(widgets.NewQTreeWidgetItem2([]string{grp.Name}, 0))
			}
		}
	})
	group.SetEnabled(false)

	add = widgets.NewQAction(nil)
	add.SetIcon(gui.NewQIcon5("icons/key.svg"))
	add.SetToolTip("Add new entry")
	add.ConnectTriggered(func(bool) {
		log.Println("New entry")
		// controller.CreateSecret("test")

	})
	add.SetEnabled(false)

	save = widgets.NewQAction(nil)
	save.SetIcon(gui.NewQIcon5("icons/save.svg"))
	save.SetToolTip("Save")
	save.ConnectTriggered(func(bool) {
		log.Println("Save")
	})
	save.SetEnabled(false)

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
	tool.AddWidget(line2)
	tool.InsertAction(nil, sub)
	tool.InsertAction(nil, group)
	tool.InsertAction(nil, add)

	return tool
}

func createSideMenu() *widgets.QWidget {
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

	layout := widgets.NewQHBoxLayout2(widget)
	layout.SetContentsMargins(0, 0, 0, 0)
	layout.SetSpacing(0)
	layout.AddWidget(tree, 0, 0)

	return widget
}

func createMain() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetStyleSheet("background-color: #FFFFFF;")

	l1 := widgets.NewQLabel(nil, 0)
	l1.SetText("Home")

	layout := widgets.NewQHBoxLayout2(widget)
	layout.AddWidget(l1, 0, 0)

	return widget
}

func getPassword(file string) string {
	dialog := widgets.NewQInputDialog(nil, 0)
	dialog.SetWindowTitle("Create master key")
	dialog.SetLabelText(fmt.Sprintf("Database: %s\n\nSpecify a new master key, which will be used to encrypt the database.\n\nRemember the master key that you enter, \nif you lose it you will not be able to open the database.", file))
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

func getSecretGroup() string {
	dialog := widgets.NewQInputDialog(nil, 0)
	dialog.SetWindowTitle("Create secret group")
	dialog.SetLabelText("Specify a name for the secret group.")
	dialog.SetOkButtonText("Ok")
	dialog.SetCancelButtonText("Cancel")
	dialog.SetTextEchoMode(0)
	dialog.SetInputMode(widgets.QInputDialog__TextInput)
	dialog.SetModal(true)
	dialog.Show()
	if dialog.Exec() == 1 {
		return dialog.TextValue()
	}
	return ""
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

func main() {
	log.Println("Start application")
	app := widgets.NewQApplication(len(os.Args), os.Args)
	window := widgets.NewQMainWindow(nil, 0)
	icon := gui.NewQIcon5("icons/pepega.png")
	window.SetWindowIcon(icon)
	window.SetMinimumSize2(800, 600)
	window.SetWindowTitle("Password manager")
	menu := createMenu()
	window.SetMenuBar(menu)
	tool := createToolBar()
	window.AddToolBar(core.Qt__TopToolBarArea, tool)
	central := widgets.NewQWidget(nil, 0)
	mainLayout := widgets.NewQVBoxLayout2(central)
	side := createSideMenu()
	main := createMain()
	splitter := widgets.NewQSplitter(nil)
	splitter.AddWidget(side)
	splitter.AddWidget(main)
	mainLayout.AddWidget(splitter, 0, 0)
	window.SetCentralWidget(central)
	window.Show()
	app.Exec()
}

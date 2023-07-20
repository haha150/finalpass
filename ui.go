package main

import (
	"fmt"
	"log"
	"os"

	"password-manager/controller"
	"password-manager/security"

	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

var tree *widgets.QTreeWidget = nil

func createMenu() *widgets.QMenuBar {
	menu := widgets.NewQMenuBar(nil)
	file := menu.AddMenu2("File")

	newDatabase := widgets.NewQAction(nil)
	newDatabase.SetIcon(gui.NewQIcon5("icons/category.svg"))
	newDatabase.SetText("New database")
	newDatabase.ConnectTriggered(func(bool) {
		fmt.Println("New database")
		controller.CreateDatabase("test")
	})

	openDatabase := widgets.NewQAction(nil)
	openDatabase.SetIcon(gui.NewQIcon5("icons/open.svg"))
	openDatabase.SetText("Open database")
	openDatabase.ConnectTriggered(func(bool) {
		fmt.Println("Open database")
		controller.OpenDatabase()
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

	save := widgets.NewQAction(nil)
	save.SetIcon(gui.NewQIcon5("icons/save.svg"))
	save.SetToolTip("Save")
	save.ConnectTriggered(func(bool) {
		log.Println("Save")
	})

	category := widgets.NewQAction(nil)
	category.SetIcon(gui.NewQIcon5("icons/category.svg"))
	category.SetToolTip("Add new category")
	category.ConnectTriggered(func(bool) {
		log.Println("New category")
		log.Println(tree.CurrentItem().Text(0))
		log.Println(tree.CurrentItem().Parent().Text(0))
		if tree.CurrentItem().Parent().Text(0) == "" {
			cat, err := controller.CreateCategory(tree.CurrentItem().Text(0), "abcd")
			if err != nil {
				log.Println(err)
			} else {
				tree.CurrentItem().AddChild(widgets.NewQTreeWidgetItem2([]string{cat.Name}, 0))
			}
		} else {
			cat, err := controller.CreateCategory(tree.CurrentItem().Parent().Text(0), "abcd2")
			if err != nil {
				log.Println(err)
			} else {
				tree.CurrentItem().Parent().AddChild(widgets.NewQTreeWidgetItem2([]string{cat.Name}, 0))
			}
		}
	})

	add := widgets.NewQAction(nil)
	add.SetIcon(gui.NewQIcon5("icons/add.svg"))
	add.SetToolTip("Add new entry")
	add.ConnectTriggered(func(bool) {
		log.Println("New entry")
		// controller.CreateSecret("test")
	})

	remove := widgets.NewQAction(nil)
	remove.SetIcon(gui.NewQIcon5("icons/remove.svg"))
	remove.SetToolTip("Remove")
	remove.ConnectTriggered(func(bool) {
		log.Println("Remove")
	})

	tool.InsertAction(nil, save)
	tool.InsertAction(nil, category)
	tool.InsertAction(nil, add)
	tool.InsertAction(nil, remove)

	return tool
}

func createSideMenu() *widgets.QWidget {
	widget := widgets.NewQWidget(nil, 0)
	widget.SetFixedWidth(100)
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

	databases, err := controller.GetAllDatabases()
	if err != nil {
		log.Println(err)
		panic(err) // handle this in a popup
	}
	for _, database := range databases {
		parent := widgets.NewQTreeWidgetItem2([]string{database.Name}, 0)
		tree.AddTopLevelItem(parent)
		log.Println(database.Categories)
		for _, category := range database.Categories {
			child := widgets.NewQTreeWidgetItem2([]string{category.Name}, 0)
			parent.AddChild(child)
		}
		parent.SetExpanded(true)
	}

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

func main() {
	log.Println("Init application")
	controller.Init()
	controller.CreateDatabaseAndCategoryIfNotExist()
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
	security.Crypted()
	mainLayout.AddWidget(splitter, 0, 0)
	window.SetCentralWidget(central)
	window.Show()
	app.Exec()
}

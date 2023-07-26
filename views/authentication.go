package views

import (
	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/widgets"
)

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

func Login() (string, string) {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Login")
	layout := widgets.NewQVBoxLayout2(dialog)
	dialog.SetLayout(layout)
	layout.AddWidget(widgets.NewQLabel2("Email", nil, 0), 0, core.Qt__AlignLeft)
	email := widgets.NewQLineEdit(nil)
	email.SetPlaceholderText("Email")
	layout.AddWidget(email, 0, 0)
	layout.AddWidget(widgets.NewQLabel2("Password", nil, 0), 0, core.Qt__AlignLeft)
	password := widgets.NewQLineEdit(nil)
	password.SetPlaceholderText("Password")
	password.SetEchoMode(2)
	layout.AddWidget(password, 0, 0)
	checkbox := widgets.NewQCheckBox(nil)
	checkbox.SetText("Show password")
	checkbox.ConnectStateChanged(func(state int) {
		if state == int(core.Qt__Checked) {
			password.SetEchoMode(0)
		} else {
			password.SetEchoMode(2)
		}
	})
	layout.AddWidget(checkbox, 0, core.Qt__AlignLeft)
	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok | widgets.QDialogButtonBox__Cancel)
	buttons.ConnectAccepted(func() {
		if email.Text() != "" && password.Text() != "" {
			dialog.Accept()
		} else {
			showError("Email or password is missing!")
		}
	})
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)
	dialog.SetModal(true)
	dialog.Show()
	if dialog.Exec() == int(widgets.QDialog__Accepted) {
		return email.Text(), password.Text()
	}
	return "", ""
}

func Register() (string, string) {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Register")
	layout := widgets.NewQVBoxLayout2(dialog)
	dialog.SetLayout(layout)
	layout.AddWidget(widgets.NewQLabel2("Email", nil, 0), 0, core.Qt__AlignLeft)
	email := widgets.NewQLineEdit(nil)
	email.SetPlaceholderText("Email")
	layout.AddWidget(email, 0, 0)
	layout.AddWidget(widgets.NewQLabel2("Password", nil, 0), 0, core.Qt__AlignLeft)
	password := widgets.NewQLineEdit(nil)
	password.SetPlaceholderText("Password")
	password.SetEchoMode(2)
	layout.AddWidget(password, 0, 0)
	checkbox := widgets.NewQCheckBox(nil)
	checkbox.SetText("Show password")
	checkbox.ConnectStateChanged(func(state int) {
		if state == int(core.Qt__Checked) {
			password.SetEchoMode(0)
		} else {
			password.SetEchoMode(2)
		}
	})
	layout.AddWidget(checkbox, 0, core.Qt__AlignLeft)
	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok | widgets.QDialogButtonBox__Cancel)
	buttons.ConnectAccepted(func() {
		if email.Text() != "" && password.Text() != "" {
			dialog.Accept()
		} else {
			showError("Email or password is missing!")
		}
	})
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)
	dialog.SetModal(true)
	dialog.Show()
	if dialog.Exec() == int(widgets.QDialog__Accepted) {
		return email.Text(), password.Text()
	}
	return "", ""
}

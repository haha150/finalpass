package views

import (
	"desktop/controller"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"os"

	"desktop/models"

	"github.com/skip2/go-qrcode"
	"github.com/therecipe/qt/core"
	"github.com/therecipe/qt/gui"
	"github.com/therecipe/qt/widgets"
)

func Login() (string, string, error) {
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
	totpLabel := widgets.NewQLabel2("Code", nil, 0)
	totpLabel.SetVisible(false)
	layout.AddWidget(totpLabel, 0, core.Qt__AlignLeft)
	totp := widgets.NewQLineEdit(nil)
	totp.SetPlaceholderText("Code")
	totp.SetVisible(false)
	layout.AddWidget(totp, 0, 0)
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
	var token string = ""
	buttons.ConnectAccepted(func() {
		if email.Text() != "" && password.Text() != "" {
			var data []byte
			if totp.Text() != "" {
				data = []byte(fmt.Sprintf(`{"username":"%s","password":"%s","totp":"%s"}`, email.Text(), password.Text(), totp.Text()))
			} else {
				data = []byte(fmt.Sprintf(`{"username":"%s","password":"%s"}`, email.Text(), password.Text()))
			}
			res, err := controller.SendRequest(fmt.Sprintf("%s/login", models.Url), "POST", data, "")
			if err != nil {
				showError(err.Error())
			} else {
				defer res.Body.Close()
				body, err := io.ReadAll(res.Body)
				if err != nil {
					log.Println(err)
					return
				}
				var data map[string]interface{}
				err2 := json.Unmarshal([]byte(body), &data)
				if err2 != nil {
					log.Println(err2)
					return
				}
				if res.StatusCode == 200 {
					showInfo(fmt.Sprintf("%s as %s.", data["message"].(string), email.Text()))
					token = data["token"].(string)
					dialog.Accept()
				} else if res.StatusCode == 405 {
					showInfo("Authenticator code required!")
					totpLabel.SetVisible(true)
					totp.SetVisible(true)
				} else {
					showError("Wrong email or password!")
				}
			}
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
		return email.Text(), token, nil
	}
	return "", "", fmt.Errorf("Login failed")
}

func Register() bool {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Register")
	layout := widgets.NewQVBoxLayout2(dialog)
	dialog.SetLayout(layout)
	label := widgets.NewQLabel2("Password must contain at least:\n- 8 characters\n- 1 uppercase character\n- 1 lowercase character\n- 1 number\n- 1 special character", nil, 0)
	layout.AddWidget(label, 0, core.Qt__AlignLeft)
	layout.AddWidget(widgets.NewQLabel2("Email", nil, 0), 0, core.Qt__AlignLeft)
	email := widgets.NewQLineEdit(nil)
	email.SetPlaceholderText("Email")
	layout.AddWidget(email, 0, 0)
	layout.AddWidget(widgets.NewQLabel2("Password", nil, 0), 0, core.Qt__AlignLeft)
	password := widgets.NewQLineEdit(nil)
	password.SetPlaceholderText("Password")
	password.SetEchoMode(2)
	repeat := widgets.NewQLineEdit(nil)
	repeat.SetPlaceholderText("Repeat")
	repeat.SetEchoMode(2)

	layout.AddWidget(password, 0, 0)
	layout.AddWidget(repeat, 0, 0)
	checkbox := widgets.NewQCheckBox(nil)
	checkbox.SetText("Show password")
	checkbox.ConnectStateChanged(func(state int) {
		if state == int(core.Qt__Checked) {
			password.SetEchoMode(0)
			repeat.SetEchoMode(0)
		} else {
			password.SetEchoMode(2)
			repeat.SetEchoMode(2)
		}
	})
	layout.AddWidget(checkbox, 0, core.Qt__AlignRight)

	label2 := widgets.NewQLabel2("Password requirements not met!", nil, 0)
	label2.SetStyleSheet("color: red")
	label2.SetVisible(false)
	layout.AddWidget(label2, 0, core.Qt__AlignLeft)

	password.ConnectTextChanged(func(text string) {
		if controller.IsPasswordSecure(password.Text()) {
			label2.SetVisible(false)
		} else {
			label2.SetVisible(true)
		}
		if password.Text() != repeat.Text() {
			password.SetStyleSheet("border: 1px solid red")
			repeat.SetStyleSheet("border: 1px solid red")
		} else {
			password.SetStyleSheet("border: 1px solid green")
			repeat.SetStyleSheet("border: 1px solid green")
		}
	})

	repeat.ConnectTextChanged(func(text string) {
		if controller.IsPasswordSecure(repeat.Text()) {
			label2.SetVisible(false)
		} else {
			label2.SetVisible(true)
		}
		if password.Text() != repeat.Text() {
			password.SetStyleSheet("border: 1px solid red")
			repeat.SetStyleSheet("border: 1px solid red")
		} else {
			password.SetStyleSheet("border: 1px solid green")
			repeat.SetStyleSheet("border: 1px solid green")
		}
	})

	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok | widgets.QDialogButtonBox__Cancel)
	buttons.ConnectAccepted(func() {
		if email.Text() != "" && password.Text() != "" && repeat.Text() != "" && password.Text() == repeat.Text() && controller.IsPasswordSecure(password.Text()) {
			data := []byte(fmt.Sprintf(`{"username":"%s","password":"%s"}`, email.Text(), password.Text()))
			res, err := controller.SendRequest(fmt.Sprintf("%s/register", models.Url), "POST", data, "")
			if err != nil {
				showError(err.Error())
			} else {
				defer res.Body.Close()
				body, err := io.ReadAll(res.Body)
				if err != nil {
					log.Println(err)
					return
				}
				var data map[string]interface{}
				err2 := json.Unmarshal([]byte(body), &data)
				if err2 != nil {
					log.Println(err2)
					return
				}
				if res.StatusCode == 201 {
					showInfo(fmt.Sprintf("Account created for %s. %s.", email.Text(), data["message"].(string)))
					dialog.Accept()
				} else {
					showError("Wrong email or password!")
				}
			}
		} else {
			showError("Email or password is missing or passwords dont match!")
		}
	})
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)
	dialog.SetModal(true)
	dialog.Show()
	return dialog.Exec() == int(widgets.QDialog__Accepted)
}

func Settings(user *models.User) {
	dialog := widgets.NewQDialog(nil, 0)
	dialog.SetWindowTitle("Settings")
	layout := widgets.NewQVBoxLayout2(dialog)
	dialog.SetLayout(layout)
	layout.AddWidget(widgets.NewQLabel2("Change password", nil, 0), 0, core.Qt__AlignLeft)
	current := widgets.NewQLineEdit(nil)
	current.SetPlaceholderText("Current password")
	current.SetEchoMode(2)
	password := widgets.NewQLineEdit(nil)
	password.SetPlaceholderText("New password")
	password.SetEchoMode(2)
	repeat := widgets.NewQLineEdit(nil)
	repeat.SetPlaceholderText("Repeat new password")
	repeat.SetEchoMode(2)
	checkbox := widgets.NewQCheckBox(nil)
	checkbox.SetText("Show password")
	checkbox.ConnectStateChanged(func(state int) {
		if state == int(core.Qt__Checked) {
			current.SetEchoMode(0)
			password.SetEchoMode(0)
			repeat.SetEchoMode(0)
		} else {
			current.SetEchoMode(2)
			password.SetEchoMode(2)
			repeat.SetEchoMode(2)
		}
	})
	layout.AddWidget(current, 0, 0)
	layout.AddWidget(password, 0, 0)
	layout.AddWidget(repeat, 0, 0)
	layout.AddWidget(checkbox, 0, core.Qt__AlignLeft)
	passwordButton := widgets.NewQPushButton2("Change", nil)
	passwordButton.ConnectClicked(func(checked bool) {
		if password.Text() != repeat.Text() {
			showError("Passwords dont match!")
			return
		}
		data := []byte(fmt.Sprintf(`{"currentpassword":"%s","newpassword":"%s"}`, current.Text(), password.Text()))
		res, err := controller.SendRequest(fmt.Sprintf("%s/user/password", models.Url), "POST", data, user.Token)
		if err != nil {
			showError(err.Error())
		} else {
			defer res.Body.Close()
			body, err := io.ReadAll(res.Body)
			if err != nil {
				log.Println(err)
				return
			}
			var data map[string]interface{}
			err2 := json.Unmarshal([]byte(body), &data)
			if err2 != nil {
				log.Println(err2)
				return
			}
			if res.StatusCode == 200 {
				showInfo(data["message"].(string))
			} else {
				showError("Wrong password!")
			}
		}
	})
	layout.AddWidget(passwordButton, 0, core.Qt__AlignRight)
	separator := widgets.NewQFrame(nil, 0)
	separator.SetFrameShape(widgets.QFrame__HLine)
	separator.SetFrameShadow(widgets.QFrame__Sunken)
	layout.AddWidget(separator, 0, 0)
	mfaCheckbox := widgets.NewQCheckBox(nil)
	mfaCheckbox.SetText("Enable 2FA")
	if user.Totp {
		mfaCheckbox.SetCheckState(core.Qt__Checked)
	} else {
		mfaCheckbox.SetCheckState(core.Qt__Unchecked)
	}
	mfaCheckbox.ConnectStateChanged(func(state int) {
		if state == int(core.Qt__Checked) {
			res, err := controller.SendRequest(fmt.Sprintf("%s/otp/generate", models.Url), "GET", nil, user.Token)
			if err != nil {
				showError(err.Error())
			} else {
				defer res.Body.Close()
				body, err := io.ReadAll(res.Body)
				if err != nil {
					log.Println(err)
					return
				}
				var data map[string]interface{}
				err2 := json.Unmarshal([]byte(body), &data)
				if err2 != nil {
					log.Println(err2)
					return
				}
				if res.StatusCode == 200 {
					qr := data["qr"].(string)
					message := data["message"].(string)
					dialog := widgets.NewQDialog(nil, 0)
					dialog.SetWindowTitle("QR code")
					layout := widgets.NewQVBoxLayout2(dialog)
					layout.AddWidget(widgets.NewQLabel2("Scan the QR code below with your authenticator app", nil, 0), 0, core.Qt__AlignLeft)
					dialog.SetLayout(layout)
					err := qrcode.WriteFile(qr, qrcode.Medium, 256, "qr.png")
					if err != nil {
						log.Println(err)
						return
					}
					pixmap := gui.NewQPixmap()
					pixmap.Load("qr.png", "PNG", 0)
					os.Remove("qr.png")
					label := widgets.NewQLabel(nil, 0)
					label.SetPixmap(pixmap)
					layout.AddWidget(label, 0, core.Qt__AlignCenter)
					label2 := widgets.NewQLabel2(message, nil, 0)
					layout.AddWidget(label2, 0, core.Qt__AlignCenter)
					buttons := widgets.NewQDialogButtonBox(nil)
					buttons.SetOrientation(core.Qt__Horizontal)
					buttons.SetStandardButtons(widgets.QDialogButtonBox__Ok)
					buttons.ConnectAccepted(func() {
						dialog.Accept()
					})
					buttons.ConnectRejected(func() {
						dialog.Reject()
					})
					layout.AddWidget(buttons, 0, core.Qt__AlignRight)
					dialog.SetModal(true)
					dialog.Show()
					dialog.Exec()
				}
			}
		}
	})
	layout.AddWidget(mfaCheckbox, 0, core.Qt__AlignLeft)
	buttons := widgets.NewQDialogButtonBox(nil)
	buttons.SetOrientation(core.Qt__Horizontal)
	buttons.SetStandardButtons(widgets.QDialogButtonBox__Cancel)
	buttons.ConnectRejected(func() {
		dialog.Reject()
	})
	layout.AddWidget(buttons, 0, core.Qt__AlignRight)
	dialog.SetModal(true)
	dialog.Show()
	dialog.Exec()
}

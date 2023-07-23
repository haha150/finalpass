package main

import (
	"password-manager/security"
)

func main() {
	security.Enc("pass", "wtfisthis")
	security.EncryptFile("pass", "ab.tst", false)
	security.Decrypt("pass", "ab.tst.enc", "ab.tst.dec")
	security.Decrypt("pass", "ab.tst.enc2", "ab.tst.dec2")
	// b := controller.GenerateStrongPassword(20)
	// fmt.Println(b)
	// a := security.CheckPasswordStrength("J@vaScr!pt12345")
	// fmt.Println(a)
	// c := security.CheckPasswordStrength(b)
	// fmt.Println(c)
}

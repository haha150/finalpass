package main

import (
	"log"
	"password-manager/security"
)

func main() {
	txt, err := security.EncryptText("pass", "wtfisthis")
	log.Println(txt)
	log.Println(err)
	a, b := security.DecryptText("pass", txt)
	log.Println(a)
	log.Println(b)
	// security.EncryptFile("pass", "ab.tst", false)
	// security.Decrypt("pass", "ab.tst.enc", "ab.tst.dec")
	// security.Decrypt("pass", "ab.tst.enc2", "ab.tst.dec2")
	// b := controller.GenerateStrongPassword(20)
	// fmt.Println(b)
	// a := security.CheckPasswordStrength("J@vaScr!pt12345")
	// fmt.Println(a)
	// c := security.CheckPasswordStrength(b)
	// fmt.Println(c)
}

var CryptoJSAesDecrypt = function(passphrase, encrypted_json_string) {
    let obj_json = JSON.parse(encrypted_json_string);

    let encrypted = obj_json.ciphertext;
    let salt = CryptoJS.enc.Hex.parse(obj_json.salt);
    let iv = CryptoJS.enc.Hex.parse(obj_json.iv);

    let key = CryptoJS.PBKDF2(passphrase, salt, {
        hasher: CryptoJS.algo.SHA512,
        keySize: 64 / 8,
        iterations: 999
    });

    let decrypted = CryptoJS.AES.decrypt(encrypted, key, {
        iv: iv
    });

    return decrypted.toString(CryptoJS.enc.Utf8);
}
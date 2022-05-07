var p2pml = { hlsjs: { Engine: { isSupported: function() { return false; } } } };
var evalInputs = [];
var options = { file: '', path: '', decodeKey: decodeKey, pathKey: pathKey };
var eval_l = eval;
eval = function (s)
{
    evalInputs.push(s);
}

function rc4(key, str) {
    var s = [],
        j = 0,
        x, res = '';
    for (var i = 0; i < 256; i++) {
        s[i] = i
    }
    for (i = 0; i < 256; i++) {
        j = (j + s[i] + key.charCodeAt(i % key.length)) % 256;
        x = s[i];
        s[i] = s[j];
        s[j] = x
    }
    i = 0;
    j = 0;
    for (var y = 0; y < str.length; y++) {
        i = (i + 1) % 256;
        j = (j + s[i]) % 256;
        x = s[i];
        s[i] = s[j];
        s[j] = x;
        res += String.fromCharCode(str.charCodeAt(y) ^ s[(s[i] + s[j]) % 256])
    }
    return res
}

function b64DecodeUnicode(str) {
    return decodeURIComponent(atob(str).split('').map(function (c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)
    }).join(''))
}

function dec(str, key = "") {
    if (!str) {
        return null;
    }

    var num = str.substring(str.length - 1);
    str = str.substring(0, str.length - 1);
    var b = atob(str);
    var k = b.substring(0, num);
    var s = b.substr(num, b.length - num);
    return b64DecodeUnicode(rc4(k + key, s))
}
String.prototype.replaceAll = function (search, replacement) {
    var target = this;
    return target.replace(new RegExp(search, 'g'), replacement)
};

function process() {
    options.file = getFileInternal();
    options.path = getPathInternal();

    options.file = dec(options.file, options.decodeKey);
    'path' in options && options.file && (options.file = options.file.replaceAll(options.pathKey, options.path));
}
function getFile() { return options.file; }
function getPath() { return options.path; }
function getFileInternal() {
    var regex1 = /file(?::|=)decode\("(.+?)"\)/;
    var regex2 = /file(?::|=)"(.+?)"/;

    var result = evalInputs && evalInputs.map && evalInputs.map(function (i) {
        var r1 = regex1.exec(i);
        if (r1 && r1[1]) {
            return decode(r1[1]);
        }
        var r2 = regex2.exec(i);
        return r2 && r2[1];
    }).find(function (r) { return r } );
    return result;
}
function getPathInternal() {
    var regex = /path\s*(?::|=)\s*"(.+?)"/;

    var results = evalInputs && evalInputs.map && evalInputs.map(function (i) { return regex.exec(i); }).find(function (r) { return r && r[1] } );
    return results && results[1];
}
function decode(input) { return decodeUri32(input); }

function decodeUri32(_0x31e0af) {
    _0x31e0af = decodeUrl(_0x31e0af);
    if (_0x31e0af && _0x31e0af.indexOf('ttp') == -0x1) {
        for (var _0x39c3e3 = 0x0, _0x39ab3a = generateHashKey(0x1); _0x39c3e3 < _0x39ab3a[0x0].length; _0x39c3e3++) {
            _0x31e0af = _0x31e0af.replace(regExpression(_0x39ab3a[0x0][_0x39c3e3]), charExp());
            _0x31e0af = _0x31e0af.replace(regExpression(_0x39ab3a[0x1][_0x39c3e3]), _0x39ab3a[0x0][_0x39c3e3]);
            _0x31e0af = _0x31e0af.replace(charExp(0x1), _0x39ab3a[0x1][_0x39c3e3])
        }
        return decodeC(unescape(atob(_0x31e0af)));
    }
}
function charExp(_0x5a0e40) {
    var _0x28d8d3 = 0xd;
    do {
        _0x28d8d3++;
    } while (_0x28d8d3 < 0x10);_0x28d8d3 = String.fromCharCode(_0x28d8d3);
    return _0x5a0e40 ? new RegExp(_0x28d8d3,'g') : _0x28d8d3;
}
function regExpression(_0x1c66af) {
    return new RegExp(_0x1c66af,'g');
}
function decodeUrl(_0x30f622) {
    if (_0x30f622 && _0x30f622[0x0] == '=') {
        var securityKey = window.securityKey;
        for (var _0x109a11 = 0x0, _0x459d9b = securityKey, _0x30f622 = _0x30f622.substr(0x1); _0x109a11 < _0x459d9b[0x0].length; _0x109a11++) {
            _0x30f622 = _0x30f622.replace(new RegExp(_0x459d9b[0x0][_0x109a11],'g'), '--');
            _0x30f622 = _0x30f622.replace(new RegExp(_0x459d9b[0x1][_0x109a11],'g'), _0x459d9b[0x0][_0x109a11]);
            _0x30f622 = _0x30f622.replace(/--/g, _0x459d9b[0x1][_0x109a11])
        }
        return decodeURIComponent(atob(_0x30f622.replace('=', '')));
    }
    return _0x30f622;
}
function generateHashKey(_0x415eb7) {
    var _0x2091e4 = [];
    for (var _0x46fd20, _0x1b270c = 0x30; _0x1b270c < 0x7c; ) {
        _0x46fd20 = String.fromCharCode(_0x1b270c++);
        if (/[^a-z0-9]/i.test(_0x46fd20))
            continue;
        _0x2091e4.push(_0x46fd20)
    }
    ;if (!_0x415eb7) {
        _0x415eb7 = 0x1
    }
    ;return shuffle(_0x2091e4, _0x415eb7)
}
function shuffle(_0x27e216, _0x1d701a) {
    if (_0x27e216 != '') {
        var _0x1f0c0e = _0x27e216.length;
        do {
            for (var _0x25208b = 0x0; _0x25208b < _0x1f0c0e; _0x25208b++) {
                for (var _0x3feb29 = 0x0; _0x3feb29 < _0x1f0c0e; _0x3feb29++) {
                    if (_0x27e216[_0x25208b] > _0x27e216[_0x3feb29 + 0x1]) {
                        var _0x528812 = _0x27e216[_0x3feb29];
                        _0x27e216[_0x3feb29] = _0x27e216[_0x3feb29 + 0x1];
                        _0x27e216[_0x3feb29 + 0x1] = _0x528812
                    }
                }
            }
        } while (_0x1d701a-- > 0x0);
        return doubleArray(_0x27e216);
    }
    return null;
}
function doubleArray(_0x20a467) {
    let _0x2e2537 = new Array([],[]);
    let _0x5d4423 = Math.round((_0x20a467.length - 0x1) / 0x2);
    for (let _0x14a0e5 = 0x0, _0x4d9da9 = _0x20a467.length; _0x14a0e5 < _0x4d9da9; _0x14a0e5++) {
        _0x2e2537[(_0x5d4423 > _0x14a0e5) ? 0x0 : 0x1].push(_0x20a467[_0x14a0e5])
    }
    return _0x2e2537;
}
function decodeC(_0x8dc083) {
    _0x8dc083 = _0x8dc083.substr(0x0);
    var _0x2256d2 = _0x8dc083.split('!').filter(function (_0x37cfba) {
        if (_0x37cfba && typeof _0x37cfba != 'undefined') {
            return _0x37cfba.replace(/^[a-z0-9]{1,3}/i, function (_0x1a2792) {
                return Number.isInteger(_0x1a2792) ? _0x37cfba : _0x1a2792
            })
        }
    });
    var _0x25a666 = [];
    for (var _0x253c96 of _0x2256d2) {
        _0x25a666.push(String.fromCharCode(parseInt(_0x253c96, 0x24)))
    }
    return _0x25a666.join('');
}
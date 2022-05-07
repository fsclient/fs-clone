var window = i = {};
var options = {};
var $ = jQuery = t = function() {};
var location = { href: 'http://localhost' };
var navigator = { userAgent: userAgent}
var document = {};
var console = { log: function () {} };
var setTimeout = function (a, b) {a();}
Function.prototype.bind = function () { return this; };
Object.prototype.bind = function () { };
var evals = [];
var l_eval = eval;
eval = function (str) { evals.push(str); eval = l_eval; };
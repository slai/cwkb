var curDivUrls = {
    /* element : url */
};

function showLoading(el) {
    // TODO: deal with re-alignment when use resizes window while loading
    var imgWidth = 31;
    var imgHeight = 31;
    var width = el.offsetWidth;
    var height = el.offsetHeight;

    var loadingEl = document.createElement("img");
    loadingEl.className = "js-loadingEl";
    loadingEl.src = "/Images/loading.gif";
    loadingEl.style.position = "absolute";
    loadingEl.style.top = height / 2 - imgHeight / 2;
    loadingEl.style.left = width / 2 - imgWidth / 2;

    el.appendChild(loadingEl);
}

function hideLoading(el) {
    var loadingEl = null;
    /*for (var i in el.childNodes) {
        var curEl = el.childNodes[i];
        if (curEl.className == "js-loadingEl") {
            loadingEl = curEl;
            break;
        }
    }*/
    loadingEl = $(el).children(".js-loadingEl").get(0);

    if (loadingEl != null && loadingEl != undefined)
        el.removeChild(loadingEl);
}

function loadInto(el, url) {
    curDivUrls[el.id] = url;

    $(el).empty();
    showLoading(el);

    $(el).load(url, function (response, status, xhr) {
        if (status == "timeout") {
            alert("Could not connect to the server. Please reload and try again.");
            return;
        }
        else if (status == "parsererror") {
            alert("Could not parse response. Please file a bug report.");
            return;
        }
        else if (status == "error") {
            $(el).html('<b>The server returned an error. Try visiting the <a href="debug">debug</a> page for more information.</b>');
        }
        else {
            hideLoading(el);
        }
    });
}

function loadIntoLHS(url) {
    var el = document.getElementById("content-lhs");
    loadInto(el, url);

    var hashMap = parseHash(window.location.hash);
    hashMap["l"] = url;
    $.hash.go(buildHash(hashMap));
}

function loadIntoRHS(url) {
    var rhs = document.getElementById("content-rhs");
    var lhs = document.getElementById("content-lhs");
    var hide_rhs = document.getElementById("hide-rhs");

    var loadFunc = function () {
        loadInto(rhs, url);

        var hashMap = parseHash(window.location.hash);
        hashMap["r"] = url;
        $.hash.go(buildHash(hashMap));
    };

    if ($(rhs).css("display") == "none") {
        $(lhs).animate({ 'right': '50%' }, 1000);
        $(rhs).css("display", "block").animate({ 'left': '50%' }, 1000,
            function () {
                $(hide_rhs).fadeIn(1000);
                loadFunc();
            }
        );
    }
    else {
        loadFunc();
    }
}

function hideRHS() {
    var rhs = document.getElementById("content-rhs");
    var lhs = document.getElementById("content-lhs");
    var hide_rhs = document.getElementById("hide-rhs");

    $(hide_rhs).fadeOut(1000);
    $(lhs).animate({ 'right': '0' }, 1000);
    // make it hidden to avoid scrollbar popping up during animation
    $(rhs).css("overflow", "hidden");
    $(rhs).animate({ 'left': '100%' }, 1000,
            function () {
                $(rhs).css("display", "none");
                $(rhs).css("overflow", "auto");

                var hashMap = parseHash(window.location.hash);
                delete hashMap["r"];
                $.hash.go(buildHash(hashMap));
            }
        );
}

function parseHash(hash) {
    if (hash[0] == "#")
        hash = hash.substring(1, hash.length);

    var parts = hash.split("&&");
    var map = {};

    for (var i = 0; i < parts.length; i++) {
        var part = parts[i];
        if (part.length == 0)
            continue;

        // only want to split on first = ; rest of part might have more =
        var key = part.indexOf("=");
        var value = "";
        if (key > 0) {
            value = part.substring(key + 1, part.length);
            key = part.substring(0, key);
        }
        else {
            key = part;
        }

        map[key] = value;
    }

    return map;
}

function buildHash(hashMap) {
    var hash = "";

    for (var s in hashMap) {
        if (hash.length > 0)
            hash += "&&";

        hash += s + "=" + hashMap[s];
    }

    return hash;
}

function makeCollapsible(header, box, expand_img_url, collapse_img_url, initially_expanded) {
    //create button
    var button = document.createElement("img");
    button.className = "expand-button";

    $(button).click(function () {
        $(box).slideToggle("fast");

        if (button.alt == "Expand") {
            button.src = collapse_img_url;
            button.alt = "Collapse";
        }
        else {
            button.src = expand_img_url;
            button.alt = "Expand";
        }
    })

    //add button to header
    header.insertBefore(button, header.childNodes[0]);

    //set initial state
    if (initially_expanded) {
        //initially expanded
        button.src = collapse_img_url;
        button.alt = "Collapse";
        $(box).show();
    }
    else {
        //initially collapsed
        button.src = expand_img_url;
        button.alt = "Expand";
        $(box).hide();
    }
}
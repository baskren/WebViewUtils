function P42_GetPageUrl() {
    return window.location.href;
}

function P42_BootstrapBase() {
    return config.environmentVariables.UNO_BOOTSTRAP_APP_BASE;
}


function P42_HtmlPrint(html) {
    const hideFrame = document.createElement("iframe");
    hideFrame.onload = P42_SetPrint;
    hideFrame.style.position = "fixed";
    hideFrame.style.right = "0";
    hideFrame.style.bottom = "0";
    hideFrame.style.width = "0";
    hideFrame.style.height = "0";
    hideFrame.style.border = "0";
    hideFrame.srcdoc = html;
    document.body.appendChild(hideFrame);
}

function P42_SetPrint() {
    this.contentWindow.__container__ = this;
    this.contentWindow.onbeforeunload = P42_ClosePrint;
    this.contentWindow.onafterprint = P42_ClosePrint;
    this.contentWindow.focus(); // Required for IE
    this.contentWindow.print();
}

function P42_ClosePrint() {
    document.body.removeChild(this.__container__);
}

// P42_UriPrint(uri) does not work because of cross domain security

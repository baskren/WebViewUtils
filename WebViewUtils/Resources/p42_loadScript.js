function p42_loadAsScript(jsCode) {
    p42_loadAsScript_status = null;
    const script = document.createElement("script");
    script.textContent = jsCode;
    script.async = true;
    script.onload = () => {
        p42_loadAsScript_loaded = "success";
    };
    script.onerror = () => {
        p42_loadAsScript_loaded = "error";
    };
    document.body.appendChild(script);
}

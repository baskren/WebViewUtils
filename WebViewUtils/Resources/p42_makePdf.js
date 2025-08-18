function p42_makePdf(options) {
    window.p42_makePdf_result = "";
    window.p42_makeP42_error = "";

    if (typeof options !== 'undefined') {
        console.log("options: " + JSON.stringify(options));
        if (typeof options.margin !== 'undefined') {
            console.log("options.margin: " + JSON.stringify(options.margin));
            
            options.margin = [options.margin.top ?? 0, options.margin.left ?? 0, options.margin.bottom ?? 0, options.margin.right ?? 0];
            console.log("options.margin: " + JSON.stringify(options.margin));
        }
        console.log("options: " + JSON.stringify(options));
    }

    html2pdf()
        .from(document.documentElement)
        .set(options)
        .outputPdf()
        .then(function (pdf) {
            console.log('then pdf');
            window.p42_makePdf_result = btoa(pdf);
        }, function (reason) {
            console.log('then error:['+reason+']');
            window.p42_makePdf_error = reason;
        });
}

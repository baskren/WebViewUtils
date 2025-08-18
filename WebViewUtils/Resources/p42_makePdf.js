function p42_makePdf(options) {
    window.p42_makePdf_result = "";
    window.p42_makeP42_error = "";

    if (typeof options !== 'undefined' && typeof options.margin !== 'undefined') {
        console.log(options.margin);
        options.margin = [ options.margin.top, options.margin.left, options.margin.bottom, options.margin.right];
        console.log(options.margin);
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

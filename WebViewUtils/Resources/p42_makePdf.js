function p42_makePdf(options) {
    window.p42_makePdf_result = "";
    window.p42_makeP42_error = "";

    /*
    options = {
        margin: 30,
        html2canvas: { scale: 1},
        jsPDF: { unit: 'pt', format: 'letter', orientation: 'portrait' },
    };
    console.log("p42_makePdf_result(" + JSON.stringify(options) + ")");

    document.body.insertAdjacentHTML("beforeend", "p42_makePdf_result(" + JSON.stringify(options) + ")");
    */

    options = {
        margin: 30,
        filename: "p42_makePdf.pdf",
        html2canvas: { scale: 1},
        jsPDF: { unit: 'pt', format: 'letter', orientation: 'portrait' },
    };

    html2pdf()
        .set(options)
        .from(document.documentElement)
        .outputPdf()
        .then(function (pdf) {
            console.log('then pdf');
            window.p42_makePdf_result = btoa(pdf);
        }, function (reason) {
            console.log('then error:['+reason+']');
            window.p42_makePdf_error = reason;
        });
}

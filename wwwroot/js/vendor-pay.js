(function () {
    var invoiceUidInput = document.getElementById('vendorPayInvoiceUid');
    var amountInput = document.getElementById('vendorPayAmount');
    var table = document.getElementById('vendorOpenInvoicesTable');
    var selLabel = document.getElementById('selInvoiceLabel');
    var selDate = document.getElementById('selInvoiceDate');
    var selDue = document.getElementById('selInvoiceDue');
    var btnFull = document.getElementById('btnPayFullDue');
    var btnPrint = document.getElementById('btnPrintVendorInvoice');
    var btnPrintInline = document.getElementById('btnPrintVendorInvoiceInline');
    var printBase = (btnPrint && btnPrint.getAttribute('data-print-base')) || '/PurchaseInvoices/Print';
    var selectedBalance = null;

    function formatMoney(n) {
        return Number(n).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function resolveInvoiceUid() {
        var fromInput = invoiceUidInput && String(invoiceUidInput.value || '').trim();
        if (fromInput && fromInput !== '0') return fromInput;

        var selectedRow = table && table.querySelector('.vendor-invoice-row.is-selected');
        if (selectedRow) {
            var selectedUid = selectedRow.getAttribute('data-invoice-uid');
            if (selectedUid) return selectedUid;
        }

        var firstRow = table && table.querySelector('.vendor-invoice-row');
        if (firstRow) return firstRow.getAttribute('data-invoice-uid');
        return null;
    }

    function printSelectedInvoice() {
        var uid = resolveInvoiceUid();
        if (!uid) {
            alert('No purchase invoice available to print.');
            return;
        }
        if (invoiceUidInput) invoiceUidInput.value = uid;
        var url = printBase.replace(/\/$/, '') + '/' + uid;
        var w = window.open(url, '_blank', 'width=960,height=720');
        if (!w) {
            alert('Pop-up blocked. Allow pop-ups to print the invoice.');
        }
    }

    function selectRow(row) {
        if (!row || !table) return;
        var uid = row.getAttribute('data-invoice-uid');
        var balance = row.getAttribute('data-balance');
        var invoiceNo = row.getAttribute('data-invoice-no') || '';
        var vendorInvoice = row.getAttribute('data-vendor-invoice') || '';
        var date = row.getAttribute('data-date') || '';

        table.querySelectorAll('.vendor-invoice-row').forEach(function (r) {
            r.classList.toggle('is-selected', r === row);
            var radio = r.querySelector('.vendor-invoice-select');
            if (radio) radio.checked = r === row;
        });

        if (invoiceUidInput) invoiceUidInput.value = uid;
        selectedBalance = balance != null ? Number(balance) : null;
        if (amountInput && selectedBalance != null) {
            amountInput.value = selectedBalance.toFixed(2);
        }

        if (selLabel) {
            var title = vendorInvoice && vendorInvoice !== '-' ? vendorInvoice : invoiceNo;
            selLabel.textContent = '';
            selLabel.appendChild(document.createTextNode(title + ' '));
            var sub = document.createElement('span');
            sub.className = 'text-muted fw-normal';
            sub.textContent = '(' + invoiceNo + ')';
            selLabel.appendChild(sub);
        }
        if (selDate) selDate.textContent = date;
        if (selDue && selectedBalance != null) selDue.textContent = formatMoney(selectedBalance);
    }

    if (table) {
        table.addEventListener('change', function (e) {
            var radio = e.target.closest('.vendor-invoice-select');
            if (!radio) return;
            selectRow(radio.closest('.vendor-invoice-row'));
        });

        table.addEventListener('click', function (e) {
            if (e.target.closest('.vendor-invoice-select')) return;
            var row = e.target.closest('.vendor-invoice-row');
            if (!row) return;
            selectRow(row);
        });
    }

    if (btnFull) {
        btnFull.addEventListener('click', function () {
            if (amountInput && selectedBalance != null) {
                amountInput.value = selectedBalance.toFixed(2);
                amountInput.focus();
            }
        });
    }

    // Always keep Print enabled — never set disabled on these buttons.
    if (btnPrint) {
        btnPrint.disabled = false;
        btnPrint.removeAttribute('disabled');
        btnPrint.addEventListener('click', printSelectedInvoice);
    }
    if (btnPrintInline) {
        btnPrintInline.disabled = false;
        btnPrintInline.removeAttribute('disabled');
        btnPrintInline.addEventListener('click', printSelectedInvoice);
    }

    var initial = table && (table.querySelector('.vendor-invoice-row.is-selected')
        || table.querySelector('.vendor-invoice-row'));
    if (initial) selectRow(initial);
})();

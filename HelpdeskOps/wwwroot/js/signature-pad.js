// Signature pads: drawing on the canvas fills the hidden Signature input, and the form is not sent without one
document.querySelectorAll('[data-signature-pad]').forEach(function (pad) {
    const canvas = pad.querySelector('canvas');
    const input = pad.querySelector('input[name="Signature"]');
    const error = pad.querySelector('[data-signature-error]');
    const context = canvas.getContext('2d');
    let drawing = false;

    // Match the canvas to its on-screen size so strokes are not stretched
    const ratio = window.devicePixelRatio || 1;
    canvas.width = canvas.offsetWidth * ratio;
    canvas.height = canvas.offsetHeight * ratio;
    context.scale(ratio, ratio);
    context.lineWidth = 2;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#1b1f3b';

    function point(event) {
        const rect = canvas.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    canvas.addEventListener('pointerdown', function (event) {
        drawing = true;
        canvas.setPointerCapture(event.pointerId);
        const p = point(event);
        context.beginPath();
        context.moveTo(p.x, p.y);
        context.lineTo(p.x + 0.1, p.y);
        context.stroke();
    });

    canvas.addEventListener('pointermove', function (event) {
        if (!drawing) return;
        const p = point(event);
        context.lineTo(p.x, p.y);
        context.stroke();
    });

    function finish() {
        if (!drawing) return;
        drawing = false;
        input.value = canvas.toDataURL('image/png');
        error.textContent = '';
    }
    canvas.addEventListener('pointerup', finish);
    canvas.addEventListener('pointercancel', finish);

    pad.querySelector('[data-signature-clear]').addEventListener('click', function () {
        context.clearRect(0, 0, canvas.width, canvas.height);
        input.value = '';
    });

    pad.closest('form').addEventListener('submit', function (event) {
        if (!input.value) {
            event.preventDefault();
            error.textContent = 'Lütfen imzanızı atın.';
        }
    });
});

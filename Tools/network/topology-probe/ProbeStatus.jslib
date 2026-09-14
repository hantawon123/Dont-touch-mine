mergeInto(LibraryManager.library, {
  ProbeStatus: function(value) {
    var text = UTF8ToString(value);
    var output = document.getElementById('probe-status');
    if (!output) {
      output = document.createElement('pre'); output.id = 'probe-status';
      output.style = 'position:fixed;top:0;left:0;z-index:10;background:white;color:black;padding:12px;white-space:pre-wrap';
      document.body.appendChild(output);
    }
    output.textContent = text;
    fetch('/probe-event', {method:'POST', body:text}).catch(function() {});
  }
});

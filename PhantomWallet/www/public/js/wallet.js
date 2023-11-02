window.Clipboard = (function(window, document, navigator) {
    var textArea,
        copy;

    function isOS() {
        return navigator.userAgent.match(/ipad|iphone/i);
    }

    function createTextArea(text) {
        textArea = document.createElement('textArea');
        textArea.value = text;
        document.body.appendChild(textArea);
    }

    function selectText() {
        var range,
            selection;

        if (isOS()) {
            range = document.createRange();
            range.selectNodeContents(textArea);
            selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            textArea.setSelectionRange(0, 999999);
        } else {
            textArea.select();
        }
    }

    function copyToClipboard() {
        document.execCommand('copy');
        document.body.removeChild(textArea);
    }

    copy = function(text) {
        bootbox.alert("Copied to the clipboard.");
        createTextArea(text);
        selectText();
        copyToClipboard();
    };

    return {
        copy: copy
    };
})(window, document, navigator);

function RegisterName() {

    //if (!document.getElementById("stake-soul")) {
          //bootbox.alert("You need one SOUL staked to register a name!");
          //return;
    //}

    //if (parseFloat((document.getElementById("stake-soul").innerHTML).replace(/\,/g,'').slice(0, -3)) < 1) {
          //bootbox.alert("You need at least one SOUL staked to register a name!");
          //return;
    //}

    bootbox.prompt({
        title: "Insert a name for your address.<br>You need at least 1 SOUL already staked to register a name.<br>This will be unregistered automatically if you unstake all your SOUL.",
        message: "It might take a while to update your name.",
        callback:
            function (result) {

              if (result) {
                var name = result;
                if (name === "") {
                    bootbox.alert("Name can not be empty");
                    return;
                }
                if (name.length < 4 || name.length > 15) {
                    bootbox.alert("Name must be bigger than 4 letters and less than 15");
                    return;
                }
                if (name == 'anonymous' || name == 'genesis') {
                    bootbox.alert("Name can not be 'anonymous' or 'genesis'");
                    return;
                }

                var index = 0;
                while (index < name.length) {
                    var charCode = name.charCodeAt(index);
                    index++;
                    if (charCode >= 97 && charCode <= 122) {
                        continue;
                    } // lowercase allowed
                    if (charCode === 95) {
                        continue;
                    } // underscore allowed
                    if (charCode >= 48 && charCode <= 57) {
                        continue;
                    } // numbers allowed

                    bootbox.alert("Only lowercase, underscore and numbers allowed");
                    return;
                }
                $('#nameSpinner').show();
                $.post('/register',
                    {
                        name: name
                    },
                    function (returnedData) {
                        $('#nameSpinner').hide();
                        if (returnedData !== "") {
                            window.location.replace("/waiting/" + returnedData);
                            console.log("error");
                        } else {
                            window.location.replace("/error");
                        }
                        console.log("Register name tx: " + returnedData);
                    }).fail(function () {
                        console.log("error registering name");
                        $('#nameSpinner').hide();
                    });

                  } else {
                    $('#nameSpinner').hide();
                  }
            }
    });
}

$(document).ready(function() {

    $("#editPencil").click(RegisterName);
    $("#editPencil2").click(RegisterName);

    // Custom check for night mode on initial load
    onloadtoggle = true;
    if (localStorage.getItem('themesetting') == 'dark') {
        if (document.getElementById("cb4")) {
          document.getElementById("cb4").checked = true;
        }
        toggleNightMode();
        onloadtoggle = false;
    }
    // check if height already cached
    if (document.getElementById("blockheight")) {
      document.getElementById("blockheight").innerHTML = localStorage.getItem('lastheight');
    }
    // check if price already cached
    if (document.getElementById("soulprice")) {
      document.getElementById("soulprice").innerHTML = localStorage.getItem('lastsoulprice');
    }
    // check if price already cached
    if (document.getElementById("soulpriceparent")) {
      document.getElementById("soulpriceparent").innerHTML = 'SOUL/' + localStorage.getItem('currency');
    }
    getPricing();
    var pathname = window.location.pathname;
    // on all pages except login and create, getChains every 5 min and getPricing every minute
    if (pathname != '/login' && pathname != '/create') {
      getChains();
      setInterval(function() {
        getChains();
      }, 300000);
      setInterval(function() {
        getPricing();
      }, 60000);
    }
    // on login page check for latest version
    if (pathname == '/login') {

      $.get('https://soul.neoeconomy.io/getlatest.json?_=' + new Date().getTime(),

        function (data) {

            //console.log(data)

            if (document.getElementById("version")) {
              if (document.getElementById("version").innerHTML != data[0].latest) {
                document.getElementById("new-version").innerHTML = '<strong>Phantom Wallet new version available!</strong><br>You should update your version to benefit from all the latest features & fixes.<br>Head over here: <a href="https://github.com/merl111/PhantomWallet/releases/tag/v' + data[0].latest + '" target="_blank">Phantom Wallet v' + data[0].latest + '</a>'
                $("#new-version").show();
              }
            }

          }

        )
    }
});

// function getChains every 5 min to get current blockheight
function getChains() {
   $.get('/chains',
       function (returnedData) {
         //console.log(returnedData)
         if (returnedData == 'null') {
             if (document.getElementById("historynewaccount")) {
               document.getElementById("historynewaccount").innerHTML = 'RPC Connection Error. Try again later.';
             }
             if (document.getElementById("portfolionewaccount")) {
               document.getElementById("portfolionewaccount").innerHTML = 'RPC Connection Error. Try again later.';
             }
             if (document.getElementById("sendnewaccount")) {
               document.getElementById("sendnewaccount").innerHTML = 'RPC Connection Error. Try again later.';
             }
           } else {
           heightmain = '#' + numberWithCommas(JSON.parse(returnedData)[0].height);
           // if height exist and changed, flash height
           if (document.getElementById("blockheight").innerHTML != heightmain) {
             document.getElementById("blockheight").innerHTML = heightmain;
             $(document.getElementById("blockheight")).addClass('flash').delay(150).queue(function(next){
                  $(this).removeClass('flash');
                  next();
             });
           }
           // store height for later
           cacheheight = document.getElementById("blockheight").innerHTML;
           localStorage.setItem('lastheight', cacheheight);
         }
       }).fail(function(e) {
          console.log(e);
          $('#historynewaccount').hide();
          $('#portfolionewaccount').hide();
       });
}

// function getPricing soul from coingecko
function getPricing() {

  // if session storage currency values not set, set them to USD / $
  if (localStorage.getItem('currency') !== null) {
    currency = localStorage.getItem('currency');
    if (localStorage.getItem('currency') == 'EUR') {
      currencysymbol = '€';
    } else if (localStorage.getItem('currency') == 'CAD') {
      currencysymbol = 'C$';
    } else if (localStorage.getItem('currency') == 'GBP') {
      currencysymbol = '£';
    } else if (localStorage.getItem('currency') == 'JPY') {
      currencysymbol = '¥';
    } else if (localStorage.getItem('currency') == 'AUD') {
      currencysymbol = 'A$';
    } else {
      currencysymbol = '$';
    }
  } else {
    currency = 'USD';
    currencysymbol = '$';
    localStorage.setItem('currency', currency);
  }
   if (document.getElementById("soulpriceparent")) {
     document.getElementById("soulpriceparent").innerHTML = 'SOUL/' + localStorage.getItem('currency');
   }
   $.get('https://api.coingecko.com/api/v3/simple/price?ids=phantasma&vs_currencies=' + currency.toLowerCase(),
       function (returnedData) {
         soulprice = currencysymbol + numberWithCommas(returnedData.phantasma[currency.toLowerCase()]);
         if (document.getElementById("soulprice")) {
           // if price exist and changed, flash price
           if (document.getElementById("soulprice").innerHTML != soulprice) {
             document.getElementById("soulprice").innerHTML = soulprice;
             $(document.getElementById("soulprice")).addClass('flash').delay(150).queue(function(next){
                  $(this).removeClass('flash');
                  next();
             });
           }
           // store price for later
           cachesoulprice = document.getElementById("soulprice").innerHTML;
           localStorage.setItem('lastsoulprice', cachesoulprice);
         }
       }).fail(function(e) {
          console.log(e);
       });
}

// function round down to x decimals
function roundDown(number, decimals) {
    decimals = decimals || 0;
    return ( Math.floor( number * Math.pow(10, decimals) ) / Math.pow(10, decimals) );
}

// function add comma to number
function numberWithCommas(x) {
    var parts = x.toString().split(".");
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    return parts.join(".")
}

// function toggle on/off dark theme
function toggleNightMode() {
  $('.well').toggleClass('dark-mode');
  $('.pending').toggleClass('dark-mode');
  $('.sidenav').toggleClass('dark-mode');
  // Handles toggle nightmode on refresh
  if (localStorage.getItem('themesetting') == 'dark') {
      if (onloadtoggle !== true) {
          localStorage.setItem('themesetting', 'light');
      }
  } else {
      localStorage.setItem('themesetting', 'dark');
      onloadtoggle = false;
  }
}

// function toggle rpc mode
function toggleRPCMode() {

  $('#manual-text').toggleClass('highlighted');
  $('#auto-text').toggleClass('highlighted');

  if (document.getElementById("cb5").checked == true) {
    $('#rpcurl').hide();
    $('#rpcdropdown').show();
    $('#refresh-rpc').show();
  } else {
    $('#rpcurl').show();
    $('#rpcdropdown').hide();
    $('#refresh-rpc').hide();
  }

}

// function ping rpc
function ping(host, location, order, pong) {

  var started = new Date().getTime();

  var http = new XMLHttpRequest();

  http.open("GET", host + '/rpc', true);
  http.onreadystatechange = function() {
    if (http.readyState == 4 && http.status == 200) {
      var ended = new Date().getTime();

      var milliseconds = ended - started;

      if (pong != null) {

        pong(milliseconds, host, order, location);
      }
    }
  };
  try {
    http.send(null);
  } catch(exception) {
    // this is expected
  }

}

// function get rpc list
function getRPCList() {

  fetchRPCList = '';
  $('#rpcurl').hide();
  $('#rpcdropdown').find('option').remove();

  $.get('https://soul.neoeconomy.io/getpeers.json?_=' + new Date().getTime(),

    function (data) {

        //console.log(data)

        for ( var i = 0; i < data.length; i++ ) {

          ping(data[i].url, data[i].location, [i], function(m, n, p, o){

            $('#rpcdropdown').append($('<option>', {value: n + '/rpc', text: o + ' • ' + m + ' msec • ' + n + '/rpc'}));

          })

        }

    }).fail(function() {

    })

    $(".fa-sync-rpc").addClass("fa-spin");
    setTimeout(function() {
      $(".fa-sync-rpc").removeClass("fa-spin");
    }, 1000);

}

// function toggle on/off cosmic/chain swap
function toggleSwap() {
  $("#wrapper-swap-confirm").fadeTo(1, 0);
  $('#cosmicswap').toggleClass('highlighted');
  $('#chainswap').toggleClass('highlighted');

  if($('#cosmicswapdiv').css('display') === 'none')
  {
    $('#cosmicswapdiv').show();
    $('#chainswapdiv').hide();
    $('#wrapper-pending-confirm').hide();
  }
  else
  {
    $('#cosmicswapdiv').hide();
    $('#chainswapdiv').show();
    if($('#neotophantasma').css('display') === 'none' && $('#step-error-reverse').css('display') === 'none')
    {
      $('#wrapper-pending-confirm').show();
    }
  }
  $('#neoaddresslink').val('');
  $('#neohash').val('');
  $('#neoamount').val('');
  $('#neoaddress').val('');
  $('#neopassphrase').val('');
  $('#neoprivatekey').val('');
  $('#neopending').val('');
  document.getElementById("wrapper-pendingswap-confirm").innerHTML = '';
  document.getElementById("wrapper-swapaddress-confirm").innerHTML = '';
  document.getElementById("wrapper-swapaddressfail-confirm").innerHTML = '';
  $("#step-3").hide();
  $('#phantasmaamount').val('');
  $(".cosmic-calc").fadeTo("slow", 0);
  $(".cosmic-calc-title").fadeTo("slow", 0);
  $("#cosmicswapbutton").fadeTo("slow", 0);
  $("#wrapper-getrates-confirm").fadeTo("fast", 0);
  $("#wrapper-confirmrates-confirm").fadeTo("fast", 0);
}

// function toggle neo/phantasma chains
function toggleChains() {
  $("#wrapper-swap-confirm").fadeTo(1, 0);
  if ($('#neotophantasma').css('display') === 'none')
    {
      $('#neotophantasma').show();
      $('#phantasmatoneo').hide();
      $('#wrapper-pending-confirm').hide();
      $(".fa-sync-chains").addClass("fa-spin");
      document.getElementById("chainswapdesc").innerHTML = 'From NEO Blockchain to Phantasma Blockchain';
      setTimeout(function() {
        $(".fa-sync-chains").removeClass("fa-spin");
      }, 1000);
    }
    else
    {
      $('#neotophantasma').hide();
      $('#phantasmatoneo').show();
      if ($('#step-error-reverse').css('display') === 'none')
        {
          $('#wrapper-pending-confirm').show();
        }
      $(".fa-sync-chains").addClass("fa-spin");
      document.getElementById("chainswapdesc").innerHTML = 'From Phantasma Blockchain to NEO Blockchain';
      setTimeout(function() {
        $(".fa-sync-chains").removeClass("fa-spin");
      }, 1000);
    }
    $('#neoaddresslink').val('');
    $('#neohash').val('');
    $('#neoamount').val('');
    $('#neoaddress').val('');
    $('#neopassphrase').val('');
    $('#neoprivatekey').val('');
    $('#neopending').val('');
    document.getElementById("wrapper-pendingswap-confirm").innerHTML = '';
    document.getElementById("wrapper-swapaddress-confirm").innerHTML = '';
    document.getElementById("wrapper-swapaddressfail-confirm").innerHTML = '';
    $("#step-3").hide();
}

// function toggle private key / encrypted key
function toggleKeyType() {
  if ($('#neopassphrase').css('display') === 'none')
    {
      $('#neopassphrase').show();
      $(".fa-sync-neo").addClass("fa-spin");
      document.getElementById("keytypetoggle").innerHTML = 'Encrypted Key + Password';
      setTimeout(function() {
        $(".fa-sync-neo").removeClass("fa-spin");
      }, 1000);
    }
    else
    {
      $('#neopassphrase').hide();
      $(".fa-sync-neo").addClass("fa-spin");
      document.getElementById("keytypetoggle").innerHTML = 'Private Key';
      setTimeout(function() {
        $(".fa-sync-neo").removeClass("fa-spin");
      }, 1000);
    }
    $('#neopassphrase').val('');
    neopassphrase = '';
    $('#neoprivatekey').val('');
    $('#neohash').val('');
}

// function fixed decimals value
function toFixed(x) {
  if (Math.abs(x) < 1.0) {
    var e = parseInt(x.toString().split('e-')[1]);
    if (e) {
        x *= Math.pow(10,e-1);
        x = '0.' + (new Array(e)).join('0') + x.toString().substring(2);
    }
  } else {
    var e = parseInt(x.toString().split('+')[1]);
    if (e > 20) {
        e -= 20;
        x /= Math.pow(10,e);
        x += (new Array(e+1)).join('0');
    }
  }
  return x;
}
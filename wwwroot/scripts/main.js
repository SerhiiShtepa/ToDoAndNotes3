$(function () {   
    $(window).on('resize', checkWindowSize);
    $('#burger-menu-toggle').on('click', function () {
        $('#sidebar').toggleClass('sidebar-hide'); // css class toggle
        localStorage.setItem("IsSidebarShown", !$('#sidebar').hasClass('sidebar-hide'));
        checkWindowSize();
    });
    $(document).on('click', '.js-update-action-with-id', function () {
        let targetId = $(this).data('receiver-id');
        let id = $(this).data('id'); // element data
        let target = $(`#${targetId}`);

        if (target.is('form')) {
            setIdToAction(target, id);
            target.trigger('submit'); // if target is one form also submit it
        }
        else {
            let forms = target.find('form');
            forms.each(function () {
                setIdToAction($(this), id);
            });
        }

        function setIdToAction(target, id) {
            let currentAction = target.attr('action');
            let numberPattern = /\/\d+/;

            if (numberPattern.test(currentAction)) {
                target.attr('action', currentAction.replace(numberPattern, `/${id}`));
            } else {
                target.attr('action', `${currentAction}/${id}`);
            }
        }
    });
    $(document).on('click', '.js-submit-by-form-id', function () {
        let formId = $(this).data('form-id');
        let form = $(`#${formId}`);
        form.trigger('submit');
    });
    $(document).on('submit', '.js-get-partial-form', function (event) {
        event.preventDefault();

        let targetModalId = $(this).data('target-modal-id');
        let targetModal = $(`#${targetModalId}`);
        let formMethod = $(this).attr('method');
        let formAction = $(this).attr('action');
        let formData = new FormData($(this)[0]);
        let returnUrl = formData.get('returnUrl');

        // create form action URL
        let url = new URL(formAction, window.location.origin);
        let params = new URLSearchParams(url.search);
        if (!params.has('returnUrl')) {
            params.set('returnUrl', returnUrl);
        }
        url.search = params.toString();
        formAction = url.toString();

         //GET: create/edit/delete project partial
         //GET: create/edit/delete label partial
         //GET: create/edit/delete task partial
        $.ajax({
            url: formAction,
            type: formMethod,
            success: function (result) {
                targetModal.html(result);
                targetModal.find('.js-picker-input').trigger('blur');
                targetModal.css('display', 'block');
                targetModal.find('.js-textarea-auto').trigger('focus');
                targetModal.initInnerVirtualSelects = initInnerVirtualSelects;
                targetModal.initInnerQuill = initInnerQuill;
                targetModal.initInnerVirtualSelects();
                targetModal.initInnerQuill();
                console.log('hello success');

                // set openModal param
                let mainFullUrl = new URL(window.location.href);
                let mainParams = mainFullUrl.searchParams;

                mainParams.set('openModal', formAction);
                mainFullUrl.searchParams = mainParams;
                window.history.pushState({}, '', mainFullUrl.toString());
            },
            error: function (xhr, status, error) {
                window.location.pathname = '/Account/Error';
                console.log('ajax: hello get error');
            }
        });
    });
    $(document).on('submit', '.js-post-partial-form', function (event) {
        event.preventDefault();

        let targetModalId = $(this).data('target-modal-id');
        let targetModal = $(`#${targetModalId}`);
        let form = $(this);
        let formMethod = form.attr('method');
        let formAction = form.attr('action');
        let formToken = form.find('input[name="__RequestVerificationToken"]').val();
        let formData = form.serialize();

        // POST: create/edit/delete project
        // POST: create/edit/delete label
        // POST: create/edit/delete task
        $.ajax({
            url: formAction,
            type: formMethod,
            data: formData,
            headers: {
                RequestVerificationToken: formToken
            },
            success: function (result) {
                if (result.success) {
                    window.location.href = result.redirectTo;
                }
                else {
                    targetModal.html(result);
                    targetModal.find('.js-picker-input').trigger('blur');
                    targetModal.find('.js-textarea-auto').trigger('focus');
                    targetModal.initInnerVirtualSelects = initInnerVirtualSelects;
                    targetModal.initInnerQuill = initInnerQuill;
                    targetModal.initInnerVirtualSelects();
                    targetModal.initInnerQuill();
                }
            },
            error: function (xhr, status, error) {
                window.location.pathname = '/Account/Error';
                console.log('ajax: hello post error');
            }
        });
    });
    $(document).on('submit', '.js-change-temp-data', function (event) {
        event.preventDefault();

        let formAction = $(this).attr('action');
        let formData = $(this).serialize();
        let formToken = $(this).find('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: formAction,
            type: 'POST',
            data: formData,
            headers: {
                RequestVerificationToken: formToken
            },
            success: function (result) {
                if (result.success) {
                    window.location.href = result.redirectTo;
                }
            },
            error: function (xhr, status, error) {
            }
        });
    });
    $(document).on('click', '.js-task-is-done', function () {
        let isChecked = this.innerHTML.includes('radio_button_checked');

        if (isChecked) {
            this.innerHTML = 'radio_button_unchecked';
            $(this).closest('form')
            $(this).closest('form').find('input[name="Task.IsCompleted"]').val(false);
        } else {
            this.innerHTML = 'radio_button_checked';
            $(this).closest('form').find('input[name="Task.IsCompleted"]').val(true);
        }
    });
    $('.label-tools').each(function () {
        let labelsCount = $(this).children('.tools-label').length;
        var labelsToHide = $(this).children(".tools-label:gt(0)");
        labelsToHide.remove();  // if 1 + label

        if (labelsCount > 1) {
            $(this).append('+' + (labelsCount - 1));
        }
        else if (labelsCount === 1) {
            // do nothing
        }
        else {
            $(this).html(''); // empty if there is not labels
        }
    });
    $(document).on('click', function (event) {
        if (event.target.classList.contains('js-close')) {
            let url = new URL(window.location.href);
            url.searchParams.delete('openModal');
            window.history.pushState({}, '', url.toString());
        }
    });

    function checkWindowSize() {
        // Remember toggle
        if (window.visualViewport.width > 635) {
            let isSidebarShown = localStorage.getItem('IsSidebarShown');
            if (isSidebarShown === 'true') {
                $('#sidebar').removeClass('sidebar-hide');
            }
        }

        // Content behaviour
        let main = $('#main');
        let sidebarIsHidden = $('#sidebar').hasClass('sidebar-hide');

        // sidebar is shown on small screen => hide main
        if (!sidebarIsHidden && window.visualViewport.width < 635) {
            main.css('opacity', 0);
            setTimeout(() => {
                main.css('display', 'none');
            }, 200);
        } // sidebar is hidden or screen is bigger => show main
        else if (sidebarIsHidden || window.visualViewport.width > 635) {
            main.css('opacity', 1);
            setTimeout(() => {
                main.css('display', 'block');
            }, 200);
        }

        // if sidebar shown on bigger screen       
        if (!sidebarIsHidden && window.visualViewport.width > 635) {
            // smaller => 1 column
            if (window.visualViewport.width < 900) {
                $(document).find('.tasks').css('display', 'none');
                $(document).find('.notes').css('display', 'none');
                $(document).find('.tasks-with-notes').css('display', 'flex');

            } // bigger => 2 column   
            else {
                $(document).find('.tasks').css('display', 'flex');
                $(document).find('.notes').css('display', 'flex');
                $(document).find('.tasks-with-notes').css('display', 'none');
            }
        } //if sidebar is hidden
        else if (sidebarIsHidden) {
            // smaller => 1 column
            if (window.visualViewport.width < 590) {
                $(document).find('.tasks').css('display', 'none');
                $(document).find('.notes').css('display', 'none');
                $(document).find('.tasks-with-notes').css('display', 'flex');

            } // bigger => 2 column   
            else {
                $(document).find('.tasks').css('display', 'flex');
                $(document).find('.notes').css('display', 'flex');
                $(document).find('.tasks-with-notes').css('display', 'none');
            }
        }
    }
    function initInnerVirtualSelects() {
        // projectSelect
        let inner = $(this);
        let projectSelect = inner.find('.js-set-projects-virtual-select');
        if (projectSelect.length > 0) {
            let projectsOptions = projectSelect.data('target-select-list'); // array => [{"Text":"..", "Value":".."},{..},{..}]
            console.log(projectsOptions);
            let projectsSelected = projectSelect.data('target-selected');  // array => ["12"]

            VirtualSelect.init({
                ele: projectSelect.get(0),
                options: projectsOptions.map(option => ({ label: option.Text, value: option.Value })),
                hideClearButton: true,
            });
            projectSelect.get(0).setValue(projectsSelected);

            // set value to its input
            projectSelect.on('change', function () {
                let selectedProject = $(this).val();
                console.log(selectedProject);
                if (inner.find('input[name="Task.ProjectId"]').length > 0) {
                    inner.find('input[name="Task.ProjectId"]').val(selectedProject);
                } else if (inner.find('input[name="Note.ProjectId"]').length > 0) {
                    inner.find('input[name="Note.ProjectId"]').val(selectedProject);
                }
            });
        }
      
        // labels
        let labelsSelect = inner.find('.js-set-labels-virtual-select');
        if (labelsSelect.length > 0) {
            let labelsOptions = labelsSelect.data('target-select-list'); // array => [{"Text":"..", "Value":".."},{..},{..}]
            let labelsSelected = labelsSelect.data('target-selected');   // array => ["12", "17"]

            VirtualSelect.init({
                ele: labelsSelect.get(0),
                options: labelsOptions.map(option => ({ label: option.Text, value: option.Value })),
                multiple: true,
                placeholder: "Labels",
                optionsSelectedText: "labels",
                optionSelectedText: "label",
                hideClearButton: true,
            });
            labelsSelect.get(0).setValue(labelsSelected);

            // set value to its input
            labelsSelect.on('change', function () {
                let selectedOptions = $(this).get(0).getSelectedOptions();
                let selectedValues = Array.from(selectedOptions).map(option => option.value); // ['17', '27']
                inner.find('input[name="SelectedLabelsId"]').val(JSON.stringify(selectedValues));// ["17","27"]
            });
        }
    }
    function openModal() {
        /*
          Open modal:
          1. Main url: get openModal param
          2. Open modal url: path (form action)
          3. Open modal url: params (form inputs)
        */
        let urlParams = new URLSearchParams(window.location.search);
        let openModalUrl = urlParams.get('openModal');
        if (openModalUrl !== null) {
            let url = new URL(openModalUrl);
            let params = url.searchParams;
            let modalId;
            if (url.pathname.includes('Tasks')) {
                modalId = 'edit-task-modal';
            }
            else if (url.pathname.includes('Notes')) {
                modalId = 'edit-note-modal';
            }
            else if (url.pathname.includes('Projects')) {
                modalId = 'edit-project-modal';
            }
            else {
                return;
            }

            let form = $(
                `<form id="test" method="get" action=${url.pathname} class="js-get-partial-form" data-target-modal-id="${modalId}">
                <input type="hidden" name="id" value="${params.get('id')}">
                <input type="hidden" name="returnUrl" value="${params.get('returnUrl')}">
            </form>`
            );
            $('body').append(form);
            $('body').find('#test').trigger('submit');
        }
    }
    function initInnerQuill() {
        let quillElem = $(this).find('.js-quill');
        if (quillElem.length > 0) {
            let toolbarOptions = [
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'header': 1 }, { 'header': 2 }, 'blockquote', { 'list': 'bullet' }, 'image'],
                ['clean']
            ];
            let options = {
                modules: {
                    toolbar: toolbarOptions // false by default
                },
                theme: 'snow'
            };
            let quill = new Quill(quillElem.get(0), options);
            quill.on('text-change', function (delta, oldDelta, source) {
                let form = quillElem.closest('form');
                let noteDescInput = form.find('input[name="Note.NoteDescription.Description"]');
                let noteShortDescInput = form.find('input[name="Note.ShortDescription"]');
                noteDescInput.val(quill.root.innerHTML);
                noteShortDescInput.val(quill.getText(0, 150));
                //noteDescInput.val(JSON.stringify(quill.getContents()));
            });
        }    
    }

    checkWindowSize();
    openModal();
    $('.js-quill-json-to-text').each(function () {
        let quill = new Quill(document.createElement('div'));
        quill.root.innerHTML = $(this).html();
        $(this).html(quill.root.textContent);
    });
});
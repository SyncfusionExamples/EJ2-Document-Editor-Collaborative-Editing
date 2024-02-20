import { createElement } from '@syncfusion/ej2-base';
import { ActionInfo, DocumentEditor } from '@syncfusion/ej2-documenteditor';
import { Button } from '@syncfusion/ej2-buttons';
import { DropDownButton, ItemModel } from '@syncfusion/ej2-splitbuttons';
import { Tooltip } from '@syncfusion/ej2-popups';
import { Dialog } from '@syncfusion/ej2-popups';
import { Toast } from '@syncfusion/ej2-notifications';
import { ListView } from '@syncfusion/ej2-lists';

/**
 * Represents document editor title bar.
 */
export class TitleBar {
    private tileBarDiv: HTMLElement;
    private documentTitle?: HTMLElement;
    private documentTitleContentEditor?: HTMLElement;
    private print?: Button;
    private documentEditor: DocumentEditor;
    private isRtl?: boolean;
    private userList?: HTMLElement;
    public userMap: any = {};
    private dialogObj?: Dialog;
    private toastObj?: Toast;

    constructor(element: HTMLElement, docEditor: DocumentEditor, isShareNeeded: Boolean, isRtl?: boolean) {
        //initializes title bar elements.
        this.tileBarDiv = element;
        this.documentEditor = docEditor;
        this.isRtl = isRtl;
        this.initializeTitleBar(isShareNeeded);
        this.wireEvents();
    }
    private initializeTitleBar = (isShareNeeded: Boolean): void => {
        let shareText: string = "";
        let shareToolTip: string = "";
        let documentTileText: string = "";
        if (!this.isRtl) {
            shareText = 'Share';
            shareToolTip = 'Share this link';
        }
        // tslint:disable-next-line:max-line-length
        this.documentTitle = createElement('label', { id: 'documenteditor_title_name', styles: 'font-weight:400;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text' });
        let iconCss: string = 'e-de-padding-right';
        let btnFloatStyle: string = 'float:right;';
        let titleCss: string = '';
        // tslint:disable-next-line:max-line-length
        this.documentTitleContentEditor = createElement('div', { id: 'documenteditor_title_contentEditor', className: 'single-line', styles: titleCss });
        this.documentTitleContentEditor.appendChild(this.documentTitle);
        this.tileBarDiv.appendChild(this.documentTitleContentEditor);
        this.documentTitleContentEditor.setAttribute('title', documentTileText);
        let btnStyles: string = btnFloatStyle + 'background: transparent;box-shadow:none; font-family: inherit;border-color: transparent;'
            + 'border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;height:28px;font-weight:400;margin-top: 2px;';
        // tslint:disable-next-line:max-line-length
        this.print = this.addButton('e-de-icon-Print ' + iconCss, shareText, btnStyles, 'de-print', shareToolTip, false) as Button;

        //User info div
        this.userList = createElement('div', { id: 'de_userInfo', styles: 'float:right;margin-top: 3px;' });
        this.tileBarDiv.appendChild(this.userList);
        this.initDialog();
    }

    private wireEvents = (): void => {
        this.print?.element.addEventListener('click', () => {
            this.dialogObj?.show();
        });
    }
    // Updates document title.
    public updateDocumentTitle = (): void => {
        if (this.documentEditor.documentName === '') {
            this.documentEditor.documentName = 'Untitled';
        }
        if (this.documentTitle) {
            this.documentTitle.textContent = this.documentEditor.documentName;
        }
    }
    // tslint:disable-next-line:max-line-length
    private addButton(iconClass: string, btnText: string, styles: string, id: string, tooltipText: string, isDropDown: boolean, items?: ItemModel[]): Button | DropDownButton {
        let button: HTMLButtonElement = createElement('button', { id: id, styles: styles }) as HTMLButtonElement;
        this.tileBarDiv.appendChild(button);
        button.setAttribute('title', tooltipText);
        let ejButton: Button = new Button({ iconCss: 'e-de-share', content: btnText }, button);
        return ejButton;
    }

    public addUser(actionInfos: ActionInfo | ActionInfo[]): void {
        if (!(actionInfos instanceof Array)) {
            actionInfos = [actionInfos]
        }
        for (let i: number = 0; i < actionInfos.length; i++) {
            let actionInfo: ActionInfo = actionInfos[i];
            if (this.userMap[actionInfo.connectionId as string]) {
                continue;
            }
            let avatar: HTMLElement = createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser as string) });
            if (this.userList) {
                this.userList.appendChild(avatar);
            }
            this.userMap[actionInfo.connectionId as string] = avatar;
        }
    }

    public removeUser(conectionId: string): void {
        if (this.userMap[conectionId]) {
            if (this.userList) {
                this.userList.removeChild(this.userMap[conectionId]);
            }
            delete this.userMap[conectionId];
        }
    }

    private constructInitial(authorName: string): string {
        const splittedName: string[] = authorName.split(' ');
        let initials: string = '';
        for (let i: number = 0; i < splittedName.length; i++) {
            if (splittedName[i].length > 0 && splittedName[i] !== '') {
                initials += splittedName[i][0];
            }
        }
        return initials;
    }

    private initDialog() {
        this.dialogObj = new Dialog({
            header: 'Share ' + this.documentEditor.documentName + '.docx',
            animationSettings: { effect: 'None' },
            showCloseIcon: true,
            isModal: true,
            width: '500px',
            visible: false,
            buttons: [{
                click: this.copyURL.bind(this),
                buttonModel: { content: 'Copy URL', isPrimary: true }
            }],
            open: function () {
                let urlTextBox = document.getElementById("share_url") as HTMLInputElement;
                if (urlTextBox) {
                    urlTextBox.value = window.location.href;
                    urlTextBox.select();
                }
            },
            beforeOpen: () => {
                if (this.dialogObj) {
                    this.dialogObj.header = 'Share "' + this.documentEditor.documentName + '.docx"';
                }
                let dialogElement: HTMLElement = document.getElementById("defaultDialog") as HTMLElement;
                if (dialogElement) {
                    dialogElement.style.display = "block";
                }
            },
        });
        this.dialogObj.appendTo('#defaultDialog');

        this.toastObj = new Toast({
            position: {
                X: 'Right'
            },
            target: document.body
        });
        this.toastObj.appendTo('#toast_type');

    }

    private copyURL() {
        // Get the text field
        var copyText: HTMLInputElement = document.getElementById("share_url") as HTMLInputElement;

        if (copyText) {
            // Select the text field
            copyText.select();
            copyText.setSelectionRange(0, 99999); // For mobile devices

            // Copy the text inside the text field
            navigator.clipboard.writeText(copyText.value);

            let toastMessage = { title: 'Success!', content: 'Link Copied.', cssClass: 'e-toast-success', icon: 'e-success toast-icons' };
            if (this.toastObj) {
                this.toastObj.show(toastMessage);
            }
            if (this.dialogObj) {
                this.dialogObj.hide();
            }
        }
    }

    public updateUserInfo(actionInfos: any, type: string) {
        if (!(actionInfos instanceof Array)) {
            actionInfos = [actionInfos];
        }
        if (type == "removeUser") {
            if (this.userMap[actionInfos]) {
                delete this.userMap[actionInfos];
            }
        } else {
            for (var i = 0; i < actionInfos.length; i++) {
                this.userMap[actionInfos[i].connectionId] = actionInfos[i];
            }
        }
        if (this.userList) {
            this.userList.innerHTML = "";
            let keys = Object.keys(this.userMap);
            for (var i = 0; i < keys.length; i++) {
                var actionInfo = this.userMap[keys[i]];
                var avatar = createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser) });
                avatar.title = actionInfo.currentUser;
                avatar.style.cursor = 'default';
                this.userList.appendChild(avatar);
                if (keys.length > 5 && i == 4) {
                    this.addListView(keys.slice(i + 1));
                    break;
                }
            }
        }
    }
    private addListView(keys: any) {
        var avatar = createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 3px', innerHTML: '+' + (keys.length) });
        avatar.style.cursor = 'pointer';
        avatar.tabIndex = 1;
        if (this.userList)
            this.userList.appendChild(avatar);
        var dataSource = [];
        for (var i = 0; i < keys.length; i++) {
            var actionInfo = this.userMap[keys[i]];
            dataSource.push({ id: "s_0" + i, text: actionInfo.currentUser, avatar: this.constructInitial(actionInfo.currentUser) });
        }
        var listViewContainer = document.createElement('div');
        var letterAvatarList = new ListView({
            // Bind listview datasource
            dataSource: dataSource,
            // Enable header title
            showHeader: false,
            // Assign list-item template
            template: '<div class="listWrapper">' +
                '${if(avatar!=="")}' +
                '<span class="e-avatar e-avatar-xsmall e-avatar-circle">${avatar}</span>' +
                '${else}' +
                '<span class="${pic} e-avatar e-avatar-xsmall e-avatar-circle"> </span>' +
                '${/if}' +
                '<span class="collab-user-info">' +
                '${text} </span> </div>',
            // Assign sorting order
            sortOrder: 'Ascending'
        });
        letterAvatarList.appendTo(listViewContainer);
        var listViewTooltip = new Tooltip({
            cssClass: 'e-tooltip-template-css e-tooltip-menu-settings',
            //Set tooltip open mode
            opensOn: 'Focus',
            //Set tooltip content
            content: listViewContainer,
            width: "200px",
            showTipPointer: false
        });
        //Render initialized Tooltip component
        listViewTooltip.appendTo(avatar);
    }
}
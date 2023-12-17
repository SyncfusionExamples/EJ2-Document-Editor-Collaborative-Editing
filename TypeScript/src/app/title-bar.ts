import { createElement } from '@syncfusion/ej2-base';
import { ActionInfo, DocumentEditor } from '@syncfusion/ej2-documenteditor';
import { Button } from '@syncfusion/ej2-buttons';
import { DropDownButton, ItemModel } from '@syncfusion/ej2-splitbuttons';

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

    constructor(element: HTMLElement, docEditor: DocumentEditor, isShareNeeded: Boolean, isRtl?: boolean) {
        //initializes title bar elements.
        this.tileBarDiv = element;
        this.documentEditor = docEditor;
        this.isRtl = isRtl;
        this.initializeTitleBar(isShareNeeded);
        this.wireEvents();
    }
    private initializeTitleBar = (isShareNeeded: Boolean): void => {
        let printText: string = "";
        let printToolTip: string = "";
        let documentTileText: string = "";
        if (!this.isRtl) {
            printText = 'Print';
            printToolTip = 'Print this document (Ctrl+P).';
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
        this.print = this.addButton('e-de-icon-Print ' + iconCss, printText, btnStyles, 'de-print', printToolTip, false) as Button;

        //User info div
        this.userList = createElement('div', { id: 'de_userInfo', styles: 'float:right;margin-top: 3px;' });
        this.tileBarDiv.appendChild(this.userList);
    }
    private wireEvents = (): void => {
        this.print?.element.addEventListener('click', this.onPrint);
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
        let ejButton: Button = new Button({ iconCss: iconClass, content: btnText }, button);
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

    private onPrint = (): void => {
        this.documentEditor.print();
    }
}
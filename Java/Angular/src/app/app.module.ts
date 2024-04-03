import { ApplicationRef, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';

@NgModule({
    declarations: [],
    imports: [
        BrowserModule, AppRoutingModule
    ],
    providers: [],
})
export class AppModule {
    constructor(private appRef: ApplicationRef) { }

    ngDoBootstrap() {
        this.appRef.bootstrap(AppComponent);
    }
}

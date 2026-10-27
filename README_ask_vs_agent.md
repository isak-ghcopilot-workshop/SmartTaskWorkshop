# Ask mode vs Agent mode 

## Practice Ask mode

1. Switch the GitHub Copilot in VSCode Chat View to "Ask" mode, use following prompt to start:
   ```
   Create Task API
   ```
   ![](./screenshots/ask_mode_01.png)
2. If GithHub Copilot prompt to choose, select in-memory storage is fine:
   ![](./screenshots/ask_mode_02.png)
3. Wait it finished, you can see AI only produce source code on Chat conversations UI, no source code files are created. 
   ![](./screenshots/ask_mode_03.png)

Note: If you want to use Ask mode in Agent Window, the UI location is a little different, no Plan mode here:

![](./screenshots/vscode_agent_window_switch_to_ask_mode.png)

## Practice Agent mode

1. Make GitHub Copilot in VSCode Chat View using default Agent, use following prompt to start:
   ```
   Create Task API using Clean Architecture
   ```
   ![](./screenshots/agent_mode_01.png)
2. Grant permissions that Agent tries to do:
   ![](./screenshots/agent_mode_02.png)
3. Answer questions agent raise, use in-memory storage solution is fine:
   ![](./screenshots/agent_mode_03.png)
4. You will notice Agent mode did create new files:
   ![](./screenshots/agent_mode_04.png)
5. When Agent finish its task, it will generate a whole clean architecture projects:
   ![](./screenshots/agent_mode_05.png)
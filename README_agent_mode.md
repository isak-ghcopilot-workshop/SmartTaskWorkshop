# Agent Mode Development

1. From `lab3_init` git tag, first, add Auditing log feature by this prompt:
   ```text
   Add Audit Logging
   ```
   ![](./screenshots/dev_with_agent_01.png)
2. After Agent run finished, you may notice multiple source files are updated/added:
   ![](./screenshots/dev_with_agent_02.png)   
   (Note: you can revert the update descripitons on Top README.md)
3. Run `/init` to update GitHub Copilot repository level custom instruction file:
   ![](./screenshots/dev_with_agent_03.png)
4. Add Customer Management Module by this prompt:
   ```
   Create Customer Management Module
   ```
   ![](./screenshots/dev_with_agent_04.png)
5. After it finished, you will notice that a new _CustomerRepository_, _CustomerService_ following clean architecture of current project.
   ![](./screenshots/dev_with_agent_05.png)   